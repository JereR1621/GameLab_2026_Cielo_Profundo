// Recorta un efecto de sonido y lo deja listo para Unity.
//
// Los efectos de los packs casi nunca vienen como los necesita un juego: traen aire
// delante, cola de sala detrás, y muchas veces más de un golpe en el mismo archivo. Un
// clip así, disparado a cadencia de carrera, se solapa consigo mismo y se vuelve barro.
//
// Hace cuatro cosas, todas medidas y ninguna a ojo:
//   · busca dónde empieza el ataque de verdad y corta ahí, dejando un pelo de aire
//   · corta al final donde se le diga, para dejar fuera lo que venga después
//   · funde entrada y salida, porque cortar en mitad de una onda hace clic
//   · normaliza, para que dos clips que van a alternarse suenen al mismo nivel
//
// Lee WAV y FLAC. Escribe WAV de 16 bits, que es lo que Unity quiere.
//
//   node Tools/audio/recortar.js <entrada> <salida.wav> [opciones]
//
// Opciones:
//   --hasta 200        Milisegundos de clip, contados desde el ataque. Es el corte que
//                      decide si un segundo golpe del archivo entra o se queda fuera.
//   --desde 0          Milisegundos a saltar antes de buscar el ataque. Sirve cuando el
//                      archivo empieza con un golpe que no interesa.
//   --umbral 0.02      Fracción del pico que ya cuenta como ataque.
//   --aire 3           Milisegundos que se dejan ANTES del ataque.
//   --fundido 25       Milisegundos de fundido de salida.
//   --entrada 1        Milisegundos de fundido de entrada. Corto: solo evita el clic.
//   --pico -1.5        dBFS a los que se normaliza. Sin esto, dos clips que se alternan
//                      suenan uno más fuerte que el otro y parece que cojeas.
//   --mono             Mezcla a un canal.

'use strict';

const path = require('path');
const wav = require('./wav.js');

function leer(ruta) {
  if (path.extname(ruta).toLowerCase() === '.flac') {
    const flac = require('./flac.js');
    const audio = flac.leer(ruta);

    if (flac.verificar(audio) === false)
      throw new Error(`${path.basename(ruta)}: la decodificación no coincide con la huella del archivo.`);

    return audio;
  }

  return wav.leer(ruta);
}

function aMono(canales) {
  if (canales.length === 1) return canales;

  const n = canales[0].length;
  const salida = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    let s = 0;
    for (let c = 0; c < canales.length; c++) s += canales[c][i];
    salida[i] = s / canales.length;
  }
  return [salida];
}

function picoDe(canales, desde = 0, hasta = Infinity) {
  let pico = 0;
  for (const ch of canales) {
    const fin = Math.min(hasta, ch.length);
    for (let i = desde; i < fin; i++) {
      const v = Math.abs(ch[i]);
      if (v > pico) pico = v;
    }
  }
  return pico;
}

// Dónde empieza el golpe: la primera muestra que pasa del umbral. Se busca sobre el
// valor absoluto y no sobre una envolvente suavizada porque un ataque percusivo sube en
// menos de un milisegundo, y suavizar lo correría hacia atrás.
function buscarAtaque(canales, desde, umbral) {
  const limite = picoDe(canales, desde) * umbral;

  for (let i = desde; i < canales[0].length; i++)
    for (const ch of canales)
      if (Math.abs(ch[i]) >= limite) return i;

  return desde;
}

function main() {
  const [entrada, salida, ...resto] = process.argv.slice(2);

  if (!entrada || !salida) {
    console.error('Uso: node recortar.js <entrada> <salida.wav> [opciones]');
    process.exit(1);
  }

  const opt = {
    hasta: 200, desde: 0, umbral: 0.02, aire: 3,
    fundido: 25, entrada: 1, pico: -1.5, mono: false,
  };

  for (let i = 0; i < resto.length; i++) {
    const clave = resto[i].replace(/^--/, '');
    if (clave === 'mono') { opt.mono = true; continue; }
    if (!(clave in opt)) throw new Error(`Opción desconocida: ${resto[i]}`);
    opt[clave] = parseFloat(resto[++i]);
  }

  const audio = leer(entrada);
  const f = audio.frecuencia;
  let canales = opt.mono ? aMono(audio.muestras) : audio.muestras;

  const ms = n => Math.round(n * f / 1000);

  const ataque = buscarAtaque(canales, ms(opt.desde), opt.umbral);
  const inicio = Math.max(0, ataque - ms(opt.aire));
  const fin = Math.min(canales[0].length, inicio + ms(opt.hasta));
  const largo = fin - inicio;

  const recortado = canales.map(ch => {
    const salida = new Float32Array(largo);
    for (let i = 0; i < largo; i++) salida[i] = ch[inicio + i];
    return salida;
  });

  // Fundidos con coseno elevado y no lineales: un fundido lineal deja un codo audible
  // justo donde el oído es más sensible, al principio y al final del sonido.
  const nEntrada = Math.min(ms(opt.entrada), largo);
  const nSalida = Math.min(ms(opt.fundido), largo);

  for (const ch of recortado) {
    for (let i = 0; i < nEntrada; i++)
      ch[i] *= 0.5 - 0.5 * Math.cos(Math.PI * i / nEntrada);

    for (let i = 0; i < nSalida; i++) {
      const t = i / nSalida;
      ch[largo - nSalida + i] *= 0.5 + 0.5 * Math.cos(Math.PI * t);
    }
  }

  const picoActual = picoDe(recortado);
  const objetivo = Math.pow(10, opt.pico / 20);
  const ganancia = picoActual > 0 ? objetivo / picoActual : 1;

  for (const ch of recortado)
    for (let i = 0; i < ch.length; i++) ch[i] *= ganancia;

  wav.escribir(salida, recortado, f);

  console.log(`${path.basename(entrada)} → ${path.basename(salida)}`);
  console.log(`  ataque en ${(ataque / f * 1000).toFixed(1)} ms · corte ${(inicio / f * 1000).toFixed(1)}–${(fin / f * 1000).toFixed(1)} ms`);
  console.log(`  ${(largo / f * 1000).toFixed(0)} ms · ${recortado.length} canal(es) · ${f} Hz`);
  console.log(`  ganancia ${(20 * Math.log10(ganancia)).toFixed(1)} dB → pico ${wav.dbfs(picoDe(recortado))} dBFS`);
}

main();
