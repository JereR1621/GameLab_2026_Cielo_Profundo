// Lectura y escritura de WAV sin comprimir. Lo comparten las herramientas de audio.
//
// Las muestras siempre se manejan en punto flotante de -1 a 1, un array por canal, así
// el resto del código no tiene que pensar nunca en la profundidad de bits del origen.

'use strict';

const fs = require('fs');
const path = require('path');

function leer(ruta) {
  const buf = fs.readFileSync(ruta);

  if (buf.toString('ascii', 0, 4) !== 'RIFF' || buf.toString('ascii', 8, 12) !== 'WAVE')
    throw new Error(`${path.basename(ruta)} no es un WAV (falta la cabecera RIFF/WAVE).`);

  let formato = null;
  let datos = null;
  let pos = 12;

  while (pos + 8 <= buf.length) {
    const id = buf.toString('ascii', pos, pos + 4);
    const tam = buf.readUInt32LE(pos + 4);
    const cuerpo = pos + 8;

    if (id === 'fmt ') {
      formato = {
        codigo: buf.readUInt16LE(cuerpo),
        canales: buf.readUInt16LE(cuerpo + 2),
        frecuencia: buf.readUInt32LE(cuerpo + 4),
        blockAlign: buf.readUInt16LE(cuerpo + 12),
        bits: buf.readUInt16LE(cuerpo + 14),
      };
    } else if (id === 'data') {
      datos = buf.subarray(cuerpo, Math.min(cuerpo + tam, buf.length));
    }

    pos = cuerpo + tam + (tam % 2); // los trozos se alinean a byte par
  }

  if (!formato || !datos) throw new Error(`${path.basename(ruta)}: no encontré 'fmt ' o 'data'.`);

  const muestras = desempaquetar(datos, formato);
  return { ...formato, muestras, cuadros: muestras[0].length };
}

// ── IMA ADPCM ───────────────────────────────────────────────────────────────────
//
// Formato comprimido de 4 bits por muestra que aparece a menudo en packs de sonido.
// El algoritmo está documentado y es corto, así que vale la pena tenerlo: sin esto
// cualquier archivo ADPCM es una caja negra que no se puede medir ni convertir.
//
// La idea: cada nibble no guarda la muestra sino cuánto cambió respecto de la
// anterior, en pasos de una tabla que se agranda o achica según el propio nibble. Por
// eso hay que decodificar en orden y no se puede saltar a la mitad.

const PASOS = [
  7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
  50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253,
  279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166,
  1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428,
  4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289,
  16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
];

const INDICES = [-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8];

function desempaquetarAdpcm(datos, f) {
  const bloque = f.blockAlign;
  const bloques = Math.floor(datos.length / bloque);

  // Cada bloque arranca con cabecera de 4 bytes por canal y el resto son nibbles.
  const porBloque = (bloque - 4 * f.canales) * 2 / f.canales + 1;
  const canales = Array.from({ length: f.canales }, () => new Float32Array(bloques * porBloque));

  for (let b = 0; b < bloques; b++) {
    const base = b * bloque;

    const pred = [];
    const idx = [];
    for (let c = 0; c < f.canales; c++) {
      pred.push(datos.readInt16LE(base + c * 4));
      idx.push(datos.readUInt8(base + c * 4 + 2));
      canales[c][b * porBloque] = pred[c] / 32768;
    }

    // Mono: los nibbles van seguidos. Estéreo: en grupos de 4 bytes alternando canal.
    let salida = 1;
    let p = base + 4 * f.canales;

    while (p < base + bloque) {
      for (let c = 0; c < f.canales; c++) {
        for (let n = 0; n < 8; n++) {
          const byte = datos.readUInt8(p + (n >> 1));
          const nibble = (n & 1) ? (byte >> 4) & 0x0f : byte & 0x0f;

          let paso = PASOS[idx[c]];
          let dif = paso >> 3;
          if (nibble & 1) dif += paso >> 2;
          if (nibble & 2) dif += paso >> 1;
          if (nibble & 4) dif += paso;
          if (nibble & 8) dif = -dif;

          pred[c] = Math.max(-32768, Math.min(32767, pred[c] + dif));
          idx[c] = Math.max(0, Math.min(88, idx[c] + INDICES[nibble]));

          const destino = b * porBloque + salida + n;
          if (destino < canales[c].length) canales[c][destino] = pred[c] / 32768;
        }
        p += 4;
      }
      salida += 8;
    }
  }

  return canales;
}

function desempaquetar(datos, f) {
  if (f.codigo === 17) return desempaquetarAdpcm(datos, f);

  const bytes = f.bits / 8;
  const cuadros = Math.floor(datos.length / (bytes * f.canales));
  const canales = Array.from({ length: f.canales }, () => new Float32Array(cuadros));

  for (let i = 0; i < cuadros; i++) {
    for (let c = 0; c < f.canales; c++) {
      const p = (i * f.canales + c) * bytes;
      let v;

      if (f.codigo === 3 && f.bits === 32) v = datos.readFloatLE(p);
      else if (f.bits === 16) v = datos.readInt16LE(p) / 32768;
      else if (f.bits === 24) v = ((datos.readUInt8(p) | (datos.readUInt8(p + 1) << 8) |
                                    (datos.readInt8(p + 2) << 16))) / 8388608;
      else if (f.bits === 32) v = datos.readInt32LE(p) / 2147483648;
      else if (f.bits === 8) v = (datos.readUInt8(p) - 128) / 128;
      else throw new Error(`Profundidad no soportada: ${f.bits} bits.`);

      canales[c][i] = v;
    }
  }

  return canales;
}

// Siempre 16 bits PCM: es lo que Unity quiere y pesa la mitad que 24.
function escribir(ruta, canales, frecuencia) {
  const nCanales = canales.length;
  const cuadros = canales[0].length;
  const datos = Buffer.alloc(cuadros * nCanales * 2);

  for (let i = 0; i < cuadros; i++) {
    for (let c = 0; c < nCanales; c++) {
      const v = Math.max(-1, Math.min(1, canales[c][i]));
      datos.writeInt16LE(Math.round(v * 32767), (i * nCanales + c) * 2);
    }
  }

  const cab = Buffer.alloc(44);
  cab.write('RIFF', 0, 'ascii');
  cab.writeUInt32LE(36 + datos.length, 4);
  cab.write('WAVE', 8, 'ascii');
  cab.write('fmt ', 12, 'ascii');
  cab.writeUInt32LE(16, 16);
  cab.writeUInt16LE(1, 20);                       // PCM
  cab.writeUInt16LE(nCanales, 22);
  cab.writeUInt32LE(frecuencia, 24);
  cab.writeUInt32LE(frecuencia * nCanales * 2, 28);
  cab.writeUInt16LE(nCanales * 2, 32);
  cab.writeUInt16LE(16, 34);
  cab.write('data', 36, 'ascii');
  cab.writeUInt32LE(datos.length, 40);

  fs.writeFileSync(ruta, Buffer.concat([cab, datos]));
}

const dbfs = v => v <= 0 ? '-inf' : (20 * Math.log10(v)).toFixed(1);

module.exports = { leer, escribir, dbfs };
