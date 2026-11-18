// Detector y recortador de impactos para efectos de sonido.
//
// La idea: en una grabación de pasos, cada pisada es un pico de energía rodeado de
// silencio o casi. Se mide la envolvente del archivo, se localizan esos picos, y para
// cada uno se busca hacia atrás dónde arranca de verdad el golpe y hacia adelante
// dónde se apaga la cola. Con eso se corta cada pisada en su propio clip.
//
// Corre con Node, sin dependencias. Solo WAV sin comprimir: decodificar MP3 u OGG
// necesita un códec que no tenemos.
//
//   node Tools/audio/pasos.js analizar  <archivo.wav> [opciones]
//   node Tools/audio/pasos.js cortar    <archivo.wav> <carpeta-salida> [opciones]
//
// Opciones:
//   --umbral 0.20      Fracción del pico global a partir de la cual algo cuenta como
//                      impacto. Bajo = detecta pisadas suaves y también ruido.
//   --separacion 160   Milisegundos mínimos entre dos impactos. Evita que un solo
//                      golpe con dos crestas (talón y punta) se cuente dos veces.
//   --cola 0.02        Fracción del pico por debajo de la cual se considera apagado.
//   --previo 5         Milisegundos de aire que se dejan ANTES del ataque. Cortar
//                      justo en el transitorio suena truncado.
//   --posterior 25     Milisegundos de margen después de la cola.
//   --mono             Mezcla a un canal. Es lo que se quiere para sonido posicional.
//   --normalizar       Sube cada clip hasta dejar el pico a -1 dBFS.

'use strict';

const fs = require('fs');
const path = require('path');

// ── Lectura y escritura de WAV ──────────────────────────────────────────────────

function leerWav(ruta) {
  // Los packs de efectos vienen tanto en WAV como en FLAC. El decodificador de FLAC está
  // aparte porque es largo; acá solo se elige cuál usar según la extensión.
  if (path.extname(ruta).toLowerCase() === '.flac') {
    const audio = require('./flac.js').leer(ruta);

    // La huella del propio archivo dice si la decodificación fue exacta. Vale la pena
    // avisarlo: analizar picos sobre un audio mal decodificado da medidas sin sentido.
    const ok = require('./flac.js').verificar(audio);
    if (ok === false) throw new Error(`${path.basename(ruta)}: la decodificación no coincide con la huella del archivo.`);

    return audio;
  }

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

// Devuelve un array por canal, con las muestras en punto flotante de -1 a 1. Trabajar
// siempre en el mismo rango evita tener que pensar en la profundidad de bits después.
function desempaquetar(datos, f) {
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

// Siempre se escribe 16 bits PCM: es lo que Unity quiere para efectos cortos y pesa
// la mitad que 24.
function escribirWav(ruta, canales, frecuencia) {
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

// ── Análisis ────────────────────────────────────────────────────────────────────

// Envolvente: el valor absoluto máximo de todos los canales, muestra a muestra, y
// después suavizado. Sin suavizar, un solo pico aislado de ruido se confunde con un
// impacto real.
function envolvente(muestras, frecuencia, ventanaMs = 5) {
  const cuadros = muestras[0].length;
  const bruta = new Float32Array(cuadros);

  for (let i = 0; i < cuadros; i++) {
    let m = 0;
    for (const canal of muestras) {
      const a = Math.abs(canal[i]);
      if (a > m) m = a;
    }
    bruta[i] = m;
  }

  const ventana = Math.max(1, Math.round(frecuencia * ventanaMs / 1000));
  const suave = new Float32Array(cuadros);
  let acumulado = 0;

  for (let i = 0; i < cuadros; i++) {
    acumulado += bruta[i];
    if (i >= ventana) acumulado -= bruta[i - ventana];
    suave[i] = acumulado / Math.min(i + 1, ventana);
  }

  // Con un bucle y no con Math.max(...bruta): un archivo de pocos segundos ya trae
  // cientos de miles de muestras y desparramarlas como argumentos revienta la pila.
  let pico = 0;
  for (let i = 0; i < cuadros; i++) if (bruta[i] > pico) pico = bruta[i];

  return { bruta, suave, pico };
}

// Un impacto es un cruce por encima del umbral que ocurre pasado el tiempo de guarda
// desde el anterior. El pico se busca DESPUÉS del cruce, no en él: la muestra más
// fuerte llega unos milisegundos después del arranque del golpe.
function detectarImpactos(env, frecuencia, opt) {
  const umbral = env.pico * opt.umbral;
  const guarda = Math.round(frecuencia * opt.separacion / 1000);
  const ventanaPico = Math.round(frecuencia * 0.08);

  const impactos = [];
  let i = 0;

  while (i < env.suave.length) {
    if (env.suave[i] < umbral) { i++; continue; }

    let pico = i;
    const fin = Math.min(env.suave.length, i + ventanaPico);
    for (let j = i; j < fin; j++)
      if (env.bruta[j] > env.bruta[pico]) pico = j;

    impactos.push({ cruce: i, pico, nivel: env.bruta[pico] });
    i = pico + guarda;
  }

  return impactos;
}

// Para cada impacto: hacia atrás hasta que la envolvente cae bajo el suelo de cola,
// hacia adelante hasta lo mismo. Se topa contra los impactos vecinos para no invadirlos.
function limites(impactos, env, frecuencia, opt) {
  const suelo = env.pico * opt.cola;
  const previo = Math.round(frecuencia * opt.previo / 1000);
  const posterior = Math.round(frecuencia * opt.posterior / 1000);
  const total = env.suave.length;

  return impactos.map((imp, k) => {
    let inicio = imp.pico;
    while (inicio > 0 && env.suave[inicio] > suelo) inicio--;
    inicio = Math.max(0, inicio - previo);

    let fin = imp.pico;
    while (fin < total - 1 && env.suave[fin] > suelo) fin++;
    fin = Math.min(total, fin + posterior);

    // Sin pisar a los vecinos.
    if (k > 0) inicio = Math.max(inicio, impactos[k - 1].pico + 1);
    if (k < impactos.length - 1) fin = Math.min(fin, impactos[k + 1].cruce);

    return { ...imp, inicio, fin };
  });
}

// ── Corte ───────────────────────────────────────────────────────────────────────

function extraer(muestras, inicio, fin, frecuencia, opt) {
  let canales = muestras.map(c => Float32Array.from(c.subarray(inicio, fin)));

  if (opt.mono && canales.length > 1) {
    const mezcla = new Float32Array(canales[0].length);
    for (let i = 0; i < mezcla.length; i++) {
      let s = 0;
      for (const c of canales) s += c[i];
      mezcla[i] = s / canales.length;
    }
    canales = [mezcla];
  }

  // Fundidos cortos en las dos puntas. Un corte a cero seco produce un clic audible,
  // y es el error más común al recortar a mano.
  const entrada = Math.min(Math.round(frecuencia * 0.002), canales[0].length);
  const salida = Math.min(Math.round(frecuencia * 0.025), Math.floor(canales[0].length / 3));

  for (const c of canales) {
    for (let i = 0; i < entrada; i++) c[i] *= i / entrada;
    for (let i = 0; i < salida; i++) c[c.length - 1 - i] *= i / salida;
  }

  if (opt.normalizar) {
    let pico = 0;
    for (const c of canales) for (const v of c) pico = Math.max(pico, Math.abs(v));
    if (pico > 0.0001) {
      const g = 0.891 / pico; // -1 dBFS, dejando aire para el remuestreo de Unity
      for (const c of canales) for (let i = 0; i < c.length; i++) c[i] *= g;
    }
  }

  return canales;
}

// ── Presentación ────────────────────────────────────────────────────────────────

const dbfs = v => v <= 0 ? '-inf' : (20 * Math.log10(v)).toFixed(1);
const seg = (n, f) => (n / f).toFixed(3);

function informar(ruta, wav, env, cortes) {
  console.log(`\n${path.basename(ruta)}`);
  console.log(`  ${wav.frecuencia} Hz · ${wav.canales} canal(es) · ${wav.bits} bits · ` +
              `${seg(wav.cuadros, wav.frecuencia)} s · pico ${dbfs(env.pico)} dBFS`);
  console.log(`  ${cortes.length} impacto(s) detectado(s)\n`);

  if (cortes.length === 0) {
    console.log('  Nada superó el umbral. Probá con --umbral más bajo.\n');
    return;
  }

  console.log('   #   impacto    recorte            dura     nivel');
  console.log('  ───────────────────────────────────────────────────');

  cortes.forEach((c, i) => {
    const dur = (c.fin - c.inicio) / wav.frecuencia;
    console.log(
      `  ${String(i + 1).padStart(2)}   ` +
      `${seg(c.pico, wav.frecuencia).padStart(7)}s   ` +
      `${seg(c.inicio, wav.frecuencia)}s → ${seg(c.fin, wav.frecuencia)}s   ` +
      `${dur.toFixed(3)}s   ` +
      `${dbfs(c.nivel).padStart(6)} dBFS`);
  });

  console.log('');
}

// ── Entrada ─────────────────────────────────────────────────────────────────────

function opciones(args) {
  const o = {
    umbral: 0.20, separacion: 160, cola: 0.02,
    previo: 5, posterior: 25, mono: false, normalizar: false,
  };

  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--mono') o.mono = true;
    else if (a === '--normalizar') o.normalizar = true;
    else if (a.startsWith('--')) o[a.slice(2)] = parseFloat(args[++i]);
  }

  return o;
}

function main() {
  const [comando, entrada, ...resto] = process.argv.slice(2);

  if (!comando || !entrada) {
    console.log('Uso:\n' +
      '  node Tools/audio/pasos.js analizar <archivo.wav> [opciones]\n' +
      '  node Tools/audio/pasos.js cortar   <archivo.wav> <carpeta-salida> [opciones]\n');
    process.exit(1);
  }

  const salida = comando === 'cortar' ? resto[0] : null;
  const opt = opciones(comando === 'cortar' ? resto.slice(1) : resto);

  const wav = leerWav(entrada);
  const env = envolvente(wav.muestras, wav.frecuencia);
  const cortes = limites(detectarImpactos(env, wav.frecuencia, opt), env, wav.frecuencia, opt);

  informar(entrada, wav, env, cortes);

  if (comando !== 'cortar') return;

  if (!salida) { console.error('Falta la carpeta de salida.'); process.exit(1); }
  fs.mkdirSync(salida, { recursive: true });

  const base = path.basename(entrada, path.extname(entrada));

  cortes.forEach((c, i) => {
    const canales = extraer(wav.muestras, c.inicio, c.fin, wav.frecuencia, opt);
    const destino = path.join(salida, `${base}_${String(i + 1).padStart(2, '0')}.wav`);
    escribirWav(destino, canales, wav.frecuencia);
    console.log(`  escrito  ${destino}`);
  });

  console.log('');
}

main();
