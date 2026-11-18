// Decodificador de FLAC.
//
// Está acá por el mismo motivo que el de IMA ADPCM en wav.js: sin esto, un archivo FLAC
// es una caja negra que no se puede medir ni recortar, y encima Unity tampoco lo importa
// —solo acepta wav, mp3, ogg y aiff—, así que igual habría que convertirlo con algo.
// No hay ffmpeg ni flac.exe en esta máquina, y depender de que los haya sería dejar la
// herramienta rota el día que se trabaje en otra.
//
// FLAC es sin pérdida y funciona por predicción: en vez de guardar cada muestra, guarda
// un polinomio que predice la señal y solo el error de esa predicción, comprimido con
// códigos de Rice. Decodificar es rehacer la predicción y sumarle el error.
//
// Devuelve lo mismo que wav.leer: canales en punto flotante de -1 a 1.

'use strict';

const fs = require('fs');
const path = require('path');

// ── Lector de bits ──────────────────────────────────────────────────────────────
//
// FLAC no se alinea a bytes casi en ningún sitio: un subcuadro puede empezar a mitad de
// byte y los códigos de Rice tienen longitud variable por definición. Hace falta leer
// bit a bit, de más significativo a menos.

class Bits {
  constructor(buf) {
    this.buf = buf;
    this.pos = 0;   // en bits
  }

  leer(n) {
    let v = 0;
    for (let i = 0; i < n; i++) {
      const byte = this.buf[this.pos >> 3];
      const bit = (byte >> (7 - (this.pos & 7))) & 1;

      // Multiplicación y no desplazamiento: con n de 32 bits, v << 1 desborda el entero
      // de 32 bits con signo de JavaScript y el valor sale negativo.
      v = v * 2 + bit;
      this.pos++;
    }
    return v;
  }

  // Con signo, en complemento a dos de n bits.
  leerConSigno(n) {
    const v = this.leer(n);
    const techo = Math.pow(2, n - 1);
    return v >= techo ? v - techo * 2 : v;
  }

  // Cuántos ceros hay antes del siguiente uno. Es la parte de longitud variable de los
  // códigos de Rice.
  unario() {
    let ceros = 0;
    while (this.pos < this.buf.length * 8) {
      const byte = this.buf[this.pos >> 3];
      const bit = (byte >> (7 - (this.pos & 7))) & 1;
      this.pos++;
      if (bit) return ceros;
      ceros++;
    }
    throw new Error('FLAC: se acabó el archivo leyendo un código unario.');
  }

  alinear() {
    this.pos = (this.pos + 7) & ~7;
  }

  get finBytes() { return this.pos >> 3; }
}

// ── Tablas del formato ──────────────────────────────────────────────────────────

const FRECUENCIAS = [
  0, 88200, 176400, 192000, 8000, 16000, 22050, 24000,
  32000, 44100, 48000, 96000, -1, -2, -3, 0,
];

const PROFUNDIDADES = [0, 8, 12, 0, 16, 20, 24, 32];

// Coeficientes de los predictores fijos, por orden.
const FIJOS = [
  [],
  [1],
  [2, -1],
  [3, -3, 1],
  [4, -6, 4, -1],
];

// ── Decodificación ──────────────────────────────────────────────────────────────

function leer(ruta) {
  const buf = fs.readFileSync(ruta);

  if (buf.toString('ascii', 0, 4) !== 'fLaC')
    throw new Error(`${path.basename(ruta)} no es un FLAC (falta la marca fLaC).`);

  // Bloques de metadatos. Solo interesa STREAMINFO; el resto (etiquetas, imagen de
  // portada, tabla de búsqueda) se salta por longitud.
  let pos = 4;
  let info = null;

  for (;;) {
    const ultimo = (buf[pos] & 0x80) !== 0;
    const tipo = buf[pos] & 0x7f;
    const largo = (buf[pos + 1] << 16) | (buf[pos + 2] << 8) | buf[pos + 3];
    const cuerpo = pos + 4;

    if (tipo === 0) {
      const b = new Bits(buf.subarray(cuerpo, cuerpo + largo));
      b.leer(16); b.leer(16);          // bloque mínimo y máximo
      b.leer(24); b.leer(24);          // cuadro mínimo y máximo
      info = {
        frecuencia: b.leer(20),
        canales: b.leer(3) + 1,
        bits: b.leer(5) + 1,
        total: b.leer(36),
      };

      // Huella del audio ORIGINAL, antes de comprimir. Es lo que permite comprobar que
      // el decodificador funciona de verdad y no solo que devuelve algo que suena: si el
      // MD5 de lo que sale coincide con este, la decodificación es exacta muestra a
      // muestra. Sin esta comprobación un fallo sutil —un bit de signo, un corrimiento—
      // pasa por bueno y aparece después como un ruido raro imposible de rastrear.
      info.md5 = Buffer.from(b.buf.subarray(18, 34)).toString('hex');
    }

    pos = cuerpo + largo;
    if (ultimo) break;
  }

  if (info == null) throw new Error(`${path.basename(ruta)}: no encontré el bloque STREAMINFO.`);

  const salida = Array.from({ length: info.canales }, () =>
    new Float32Array(info.total > 0 ? info.total : 0));
  const trozos = info.total > 0 ? null : Array.from({ length: info.canales }, () => []);

  const b = new Bits(buf);
  b.pos = pos * 8;

  let escritas = 0;

  while (b.finBytes < buf.length - 2) {
    // Sincronismo: catorce unos seguidos de un cero. Si no aparece, se acabaron los
    // cuadros (lo que queda suele ser relleno o basura al final del archivo).
    if (buf[b.finBytes] !== 0xff || (buf[b.finBytes + 1] & 0xfc) !== 0xf8) break;

    const cuadro = leerCuadro(b, info);

    for (let c = 0; c < info.canales; c++) {
      const escala = Math.pow(2, cuadro.bits - 1);
      for (let i = 0; i < cuadro.tamano; i++) {
        const v = cuadro.canales[c][i] / escala;
        if (trozos) trozos[c].push(v);
        else if (escritas + i < salida[c].length) salida[c][escritas + i] = v;
      }
    }

    escritas += cuadro.tamano;
  }

  const canales = trozos ? trozos.map(t => Float32Array.from(t)) : salida;

  return {
    codigo: -1,                 // no es PCM: marca que vino de FLAC
    canales: info.canales,
    frecuencia: info.frecuencia,
    bits: info.bits,
    muestras: canales,
    cuadros: canales[0].length,
    md5: info.md5,
  };
}

// Comprueba la decodificación contra la huella que el propio archivo trae. Devuelve
// true, false, o null si el archivo no guardó huella (está permitido dejarla en ceros).
function verificar(audio) {
  if (!audio.md5 || /^0+$/.test(audio.md5)) return null;

  const bytes = audio.bits / 8;
  const escala = Math.pow(2, audio.bits - 1);
  const crudo = Buffer.alloc(audio.cuadros * audio.canales * bytes);

  for (let i = 0; i < audio.cuadros; i++) {
    for (let c = 0; c < audio.canales; c++) {
      const v = Math.round(audio.muestras[c][i] * escala);
      crudo.writeIntLE(v, (i * audio.canales + c) * bytes, bytes);
    }
  }

  const huella = require('crypto').createHash('md5').update(crudo).digest('hex');
  return huella === audio.md5;
}

function leerCuadro(b, info) {
  b.leer(14);                    // sincronismo
  b.leer(1);                     // reservado
  const porMuestra = b.leer(1);  // estrategia de bloque

  const codTamano = b.leer(4);
  const codFrecuencia = b.leer(4);
  const asignacion = b.leer(4);
  const codBits = b.leer(3);
  b.leer(1);                     // reservado

  // Número de cuadro o de muestra, codificado como UTF-8 pero de hasta 36 bits. No se
  // usa para nada acá —los cuadros vienen en orden— pero hay que consumirlo entero o
  // todo lo que sigue se lee corrido.
  saltarNumero(b);

  let tamano;
  if (codTamano === 1) tamano = 192;
  else if (codTamano >= 2 && codTamano <= 5) tamano = 576 * Math.pow(2, codTamano - 2);
  else if (codTamano === 6) tamano = b.leer(8) + 1;
  else if (codTamano === 7) tamano = b.leer(16) + 1;
  else tamano = 256 * Math.pow(2, codTamano - 8);

  if (codFrecuencia === 12) b.leer(8);
  else if (codFrecuencia === 13 || codFrecuencia === 14) b.leer(16);

  const bits = codBits === 0 ? info.bits : PROFUNDIDADES[codBits];

  b.leer(8);   // CRC de la cabecera; no se comprueba

  // Estéreo con decorrelación: uno de los dos canales guardado es en realidad la
  // diferencia entre izquierda y derecha, que comprime mucho mejor. Ese canal necesita
  // un bit más de rango, porque una resta de dos números de n bits ocupa n+1.
  const canales = [];
  const n = asignacion < 8 ? asignacion + 1 : 2;

  for (let c = 0; c < n; c++) {
    let extra = 0;
    if (asignacion === 8 && c === 1) extra = 1;   // izquierda / diferencia
    if (asignacion === 9 && c === 0) extra = 1;   // diferencia / derecha
    if (asignacion === 10 && c === 1) extra = 1;  // media / diferencia
    canales.push(leerSubcuadro(b, tamano, bits + extra));
  }

  b.alinear();
  b.leer(16);  // CRC del cuadro; no se comprueba

  return { tamano, bits, canales: recomponer(canales, asignacion, tamano) };
}

// Deshace la decorrelación estéreo.
function recomponer(canales, asignacion, tamano) {
  if (asignacion < 8) return canales;

  const a = canales[0];
  const d = canales[1];
  const izq = new Float64Array(tamano);
  const der = new Float64Array(tamano);

  for (let i = 0; i < tamano; i++) {
    if (asignacion === 8) {              // guardado: izquierda y (izquierda - derecha)
      izq[i] = a[i];
      der[i] = a[i] - d[i];
    } else if (asignacion === 9) {       // guardado: (izquierda - derecha) y derecha
      der[i] = d[i];
      izq[i] = d[i] + a[i];
    } else {                             // guardado: media y diferencia
      const dif = d[i];

      // La media se guardó dividida por dos, y esa división perdió el bit de abajo. Ese
      // bit es el mismo que el de la diferencia, así que se recupera de ahí.
      const med = a[i] * 2 + (dif < 0 ? (Math.abs(dif) % 2) : (dif % 2));
      izq[i] = (med + dif) / 2;
      der[i] = (med - dif) / 2;
    }
  }

  return [izq, der];
}

function leerSubcuadro(b, tamano, bits) {
  b.leer(1);                     // relleno
  const tipo = b.leer(6);
  const tieneDesperdiciados = b.leer(1);

  // Bits desperdiciados: si todas las muestras del bloque terminan en ceros, el
  // codificador los quita y avisa cuántos. Hay que volver a ponerlos al final.
  let desperdiciados = 0;
  if (tieneDesperdiciados) desperdiciados = b.unario() + 1;

  const efectivos = bits - desperdiciados;
  let salida;

  if (tipo === 0) {
    // Constante: una sola muestra para todo el bloque. Silencio, casi siempre.
    const v = b.leerConSigno(efectivos);
    salida = new Float64Array(tamano).fill(v);
  } else if (tipo === 1) {
    // Literal: sin comprimir. Aparece cuando la predicción no ayudaba.
    salida = new Float64Array(tamano);
    for (let i = 0; i < tamano; i++) salida[i] = b.leerConSigno(efectivos);
  } else if (tipo >= 8 && tipo <= 12) {
    salida = predictorFijo(b, tamano, efectivos, tipo - 8);
  } else if (tipo >= 32) {
    salida = predictorLineal(b, tamano, efectivos, tipo - 31);
  } else {
    throw new Error(`FLAC: tipo de subcuadro reservado (${tipo}).`);
  }

  if (desperdiciados > 0) {
    const factor = Math.pow(2, desperdiciados);
    for (let i = 0; i < tamano; i++) salida[i] *= factor;
  }

  return salida;
}

// Predictor fijo: los coeficientes están en el formato, no en el archivo. Son las
// diferencias sucesivas, que es lo que aplana una señal suave.
function predictorFijo(b, tamano, bits, orden) {
  const x = new Float64Array(tamano);
  for (let i = 0; i < orden; i++) x[i] = b.leerConSigno(bits);

  const r = leerResiduo(b, tamano, orden);
  const coef = FIJOS[orden];

  for (let i = orden; i < tamano; i++) {
    let suma = 0;
    for (let j = 0; j < orden; j++) suma += coef[j] * x[i - 1 - j];
    x[i] = r[i - orden] + suma;
  }

  return x;
}

// Predictor lineal: los coeficientes vienen en el archivo, calculados por el codificador
// para esta señal concreta. Es lo que hace que FLAC comprima de verdad.
function predictorLineal(b, tamano, bits, orden) {
  const x = new Float64Array(tamano);
  for (let i = 0; i < orden; i++) x[i] = b.leerConSigno(bits);

  const precision = b.leer(4) + 1;
  const desplazamiento = b.leerConSigno(5);

  const coef = [];
  for (let i = 0; i < orden; i++) coef.push(b.leerConSigno(precision));

  const r = leerResiduo(b, tamano, orden);
  const divisor = Math.pow(2, desplazamiento);

  for (let i = orden; i < tamano; i++) {
    let suma = 0;
    for (let j = 0; j < orden; j++) suma += coef[j] * x[i - 1 - j];

    // Math.floor y no el operador >>: la suma puede pasarse de 32 bits con órdenes
    // altos, y >> la truncaría a 32 dando basura. Para negativos, floor de la división
    // es exactamente lo que hace el desplazamiento aritmético.
    x[i] = r[i - orden] + Math.floor(suma / divisor);
  }

  return x;
}

// El error de predicción, comprimido con códigos de Rice: los valores chicos ocupan
// pocos bits y los grandes muchos, que es justo la forma que tiene un error de
// predicción bien hecho.
function leerResiduo(b, tamano, orden) {
  const metodo = b.leer(2);
  if (metodo > 1) throw new Error(`FLAC: método de residuo reservado (${metodo}).`);

  const anchoParametro = metodo === 0 ? 4 : 5;
  const escape = metodo === 0 ? 15 : 31;

  const ordenParticion = b.leer(4);
  const particiones = Math.pow(2, ordenParticion);

  const salida = new Float64Array(tamano - orden);
  let escritas = 0;

  for (let p = 0; p < particiones; p++) {
    // La primera partición lleva menos muestras: las de arranque del predictor ya se
    // leyeron aparte y no tienen residuo.
    const cuantas = ordenParticion === 0
      ? tamano - orden
      : (tamano / particiones) - (p === 0 ? orden : 0);

    const parametro = b.leer(anchoParametro);

    if (parametro === escape) {
      // Partición sin comprimir: el codificador decidió que Rice no ayudaba.
      const ancho = b.leer(5);
      for (let i = 0; i < cuantas; i++)
        salida[escritas++] = ancho === 0 ? 0 : b.leerConSigno(ancho);
      continue;
    }

    for (let i = 0; i < cuantas; i++) {
      const alto = b.unario();
      const bajo = parametro > 0 ? b.leer(parametro) : 0;

      // Multiplicación y no desplazamiento: con parámetro grande, alto << parametro
      // desborda los 32 bits de JavaScript.
      const v = alto * Math.pow(2, parametro) + bajo;

      // Zigzag: los negativos se guardan intercalados con los positivos para que el
      // valor codificado sea siempre positivo.
      salida[escritas++] = (v % 2 === 1) ? -(v + 1) / 2 : v / 2;
    }
  }

  return salida;
}

// El número de cuadro va codificado como UTF-8 pero admitiendo hasta 36 bits, así que
// hay que mirar el primer byte para saber cuántos le siguen.
function saltarNumero(b) {
  const primero = b.leer(8);
  let extra = 0;

  if ((primero & 0x80) === 0) extra = 0;
  else if ((primero & 0xe0) === 0xc0) extra = 1;
  else if ((primero & 0xf0) === 0xe0) extra = 2;
  else if ((primero & 0xf8) === 0xf0) extra = 3;
  else if ((primero & 0xfc) === 0xf8) extra = 4;
  else if ((primero & 0xfe) === 0xfc) extra = 5;
  else extra = 6;

  for (let i = 0; i < extra; i++) b.leer(8);
}

module.exports = { leer, verificar };
