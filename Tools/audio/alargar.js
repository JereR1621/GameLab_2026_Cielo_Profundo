// Alarga un trozo de un sonido sin cambiarle el tono.
//
// Ralentizar la reproducción es lo fácil, pero baja el tono con la duración: un rugido
// estirado así se convierte en otro rugido más grave. Esto hace lo otro — dura más y
// suena igual.
//
// El método es suma con solapamiento. Se corta el trozo en granos de unos noventa
// milisegundos, se les aplica una ventana de Hann, y se vuelven a pegar MÁS SEPARADOS de
// lo que estaban. Como cada grano conserva su forma de onda intacta, conserva su tono; lo
// único que cambia es cada cuánto entra el siguiente. Con la ventana de Hann y medio
// grano de solapamiento, las mitades que se superponen suman exactamente uno, así que no
// aparecen ondulaciones de volumen en las juntas.
//
// Funciona especialmente bien con sonidos ruidosos —rugidos, viento, gruñidos— porque el
// oído no puede seguirle la fase a un material sin tono definido y los artefactos del
// pegado se esconden solos. En una nota limpia se oiría metálico.
//
//   node Tools/audio/alargar.js <entrada> <salida.wav> [opciones]
//
// Opciones:
//   --desde 1000     Milisegundo a partir del cual se estira. Lo de antes queda intacto.
//   --factor 5.5     Cuántas veces más largo queda ese trozo.
//   --grano 90       Tamaño del grano en milisegundos. Grande conserva mejor los graves;
//                    pequeño responde mejor a los golpes.
//   --fundido 400    Milisegundos de fundido al final, para que no corte en seco.
//   --pico -2        dBFS a los que se normaliza el resultado.
//   --mono           Mezcla a un canal.

'use strict';

const path = require('path');
const wav = require('./wav.js');

function leer(ruta) {
  if (path.extname(ruta).toLowerCase() === '.flac') {
    const flac = require('./flac.js');
    const audio = flac.leer(ruta);
    if (flac.verificar(audio) === false)
      throw new Error(`${path.basename(ruta)}: la decodificación no coincide con la huella.`);
    return audio;
  }
  return wav.leer(ruta);
}

// Ventana de Hann periódica. Periódica y no simétrica: es la que suma exactamente uno
// con medio grano de solapamiento, y por eso no deja ondulación.
function hann(n) {
  const w = new Float32Array(n);
  for (let i = 0; i < n; i++) w[i] = 0.5 - 0.5 * Math.cos(2 * Math.PI * i / n);
  return w;
}

function estirar(canal, desde, hasta, factor, grano) {
  const N = grano;
  const saltoSalida = Math.floor(N / 2);

  // Cada grano avanza menos en el origen que en el destino: esa diferencia ES el
  // estiramiento. Con factor 2 se avanza la mitad, así que el material dura el doble.
  const saltoEntrada = Math.max(1, saltoSalida / factor);

  const largoEntrada = hasta - desde;
  const largoSalida = Math.round(largoEntrada * factor);

  const salida = new Float32Array(largoSalida + N);
  const w = hann(N);

  const granos = Math.ceil(largoSalida / saltoSalida);

  for (let g = 0; g < granos; g++) {
    const inicio = desde + Math.round(g * saltoEntrada);
    const destino = g * saltoSalida;

    for (let i = 0; i < N; i++) {
      const src = inicio + i;
      if (src >= hasta) break;
      salida[destino + i] += canal[src] * w[i];
    }
  }

  return salida.subarray(0, largoSalida);
}

function main() {
  const [entrada, salida, ...resto] = process.argv.slice(2);

  if (!entrada || !salida) {
    console.error('Uso: node alargar.js <entrada> <salida.wav> [opciones]');
    process.exit(1);
  }

  const opt = { desde: 1000, factor: 4, grano: 90, fundido: 400, pico: -2, mono: false };

  for (let i = 0; i < resto.length; i++) {
    const clave = resto[i].replace(/^--/, '');
    if (clave === 'mono') { opt.mono = true; continue; }
    if (!(clave in opt)) throw new Error(`Opción desconocida: ${resto[i]}`);
    opt[clave] = parseFloat(resto[++i]);
  }

  const audio = leer(entrada);
  const f = audio.frecuencia;
  let canales = audio.muestras;

  if (opt.mono && canales.length > 1) {
    const n = canales[0].length;
    const mezcla = new Float32Array(n);
    for (let i = 0; i < n; i++) {
      let s = 0;
      for (let c = 0; c < canales.length; c++) s += canales[c][i];
      mezcla[i] = s / canales.length;
    }
    canales = [mezcla];
  }

  const corte = Math.min(Math.round(opt.desde * f / 1000), canales[0].length);
  const grano = Math.max(64, Math.round(opt.grano * f / 1000));

  const resultado = canales.map(ch => {
    const cola = estirar(ch, corte, ch.length, opt.factor, grano);
    const salida = new Float32Array(corte + cola.length);
    salida.set(ch.subarray(0, corte), 0);
    salida.set(cola, corte);
    return salida;
  });

  const largo = resultado[0].length;
  const nFundido = Math.min(Math.round(opt.fundido * f / 1000), largo);

  for (const ch of resultado)
    for (let i = 0; i < nFundido; i++) {
      const t = i / nFundido;
      ch[largo - nFundido + i] *= 0.5 + 0.5 * Math.cos(Math.PI * t);
    }

  let pico = 0;
  for (const ch of resultado)
    for (let i = 0; i < ch.length; i++) {
      const v = Math.abs(ch[i]);
      if (v > pico) pico = v;
    }

  const ganancia = pico > 0 ? Math.pow(10, opt.pico / 20) / pico : 1;
  for (const ch of resultado)
    for (let i = 0; i < ch.length; i++) ch[i] *= ganancia;

  wav.escribir(salida, resultado, f);

  console.log(`${path.basename(entrada)} → ${path.basename(salida)}`);
  console.log(`  intacto hasta ${opt.desde} ms, resto ×${opt.factor} con granos de ${opt.grano} ms`);
  console.log(`  ${(audio.cuadros / f).toFixed(2)} s → ${(largo / f).toFixed(2)} s`);
  console.log(`  ganancia ${(20 * Math.log10(ganancia)).toFixed(1)} dB → pico ${wav.dbfs(pico * ganancia)} dBFS`);
}

main();
