// Busca dónde cortar un tema para que quede en bucle sin costura.
//
// La idea, que es la correcta: no se trata de fundir el final con el principio, sino de
// encontrar en la parte final un punto donde la música vuelve a decir lo mismo que dice
// al empezar. Si a los 96,4 segundos suena igual que a los 0, cortando ahí el salto de
// vuelta al cero es musicalmente invisible: el oyente escucha una repetición, que es lo
// que la música hace todo el tiempo.
//
// Cómo lo mide:
//   1. Toma los primeros segundos como referencia.
//   2. Reduce todo a una envolvente de 100 puntos por segundo. A resolución completa
//      esto serían billones de operaciones; sobre la envolvente son milésimas, y la
//      estructura musical —que es lo que hay que hacer coincidir— sobrevive intacta.
//   3. Correlaciona la referencia contra cada posición de la zona final y se queda con
//      la de mayor parecido.
//   4. Afina esa posición muestra a muestra en un entorno chico.
//   5. Corre el corte al cruce por cero más cercano, para que el salto no haga clic.
//
//   node Tools/audio/bucle.js <archivo.wav> <salida.wav> [opciones]
//
// Opciones:
//   --referencia 3   Segundos del comienzo que se usan como patrón a buscar.
//   --zona 45        Segundos finales donde buscar la coincidencia.
//   --minimo 40      Largo mínimo del bucle. Evita que elija un punto muy temprano.
//   --informe        Muestra los diez mejores candidatos y no escribe nada.

'use strict';

const path = require('path');
const wav = require('./wav');

const HZ_ENVOLVENTE = 100; // puntos por segundo

// Envolvente RMS por bloques. Se promedia entre canales: para encontrar la estructura
// no importa la imagen estéreo.
function envolvente(muestras, frecuencia) {
  const bloque = Math.round(frecuencia / HZ_ENVOLVENTE);
  const puntos = Math.floor(muestras[0].length / bloque);
  const env = new Float32Array(puntos);

  for (let p = 0; p < puntos; p++) {
    let suma = 0;
    for (let i = p * bloque; i < (p + 1) * bloque; i++)
      for (const canal of muestras) suma += canal[i] * canal[i];

    env[p] = Math.sqrt(suma / (bloque * muestras.length));
  }

  return env;
}

// Correlación normalizada: mide parecido de forma, no de volumen. Sin normalizar, el
// tramo más fuerte del tema ganaría siempre aunque no se parezca en nada.
function correlacion(env, refDesde, refLargo, offset) {
  let sumaA = 0, sumaB = 0, sumaAB = 0, sumaA2 = 0, sumaB2 = 0;

  for (let i = 0; i < refLargo; i++) {
    const a = env[refDesde + i];
    const b = env[offset + i];
    sumaA += a; sumaB += b;
    sumaAB += a * b; sumaA2 += a * a; sumaB2 += b * b;
  }

  const n = refLargo;
  const num = sumaAB - sumaA * sumaB / n;
  const den = Math.sqrt((sumaA2 - sumaA * sumaA / n) * (sumaB2 - sumaB * sumaB / n));
  return den < 1e-9 ? 0 : num / den;
}

// Afinado a resolución de muestra. La envolvente da el punto con precisión de 10 ms,
// que para la estructura alcanza pero para la forma de onda no.
function afinar(muestras, aproximado, radio) {
  const largo = Math.round(radio / 2);
  let mejor = aproximado, mejorPuntaje = -Infinity;

  for (let off = aproximado - radio; off <= aproximado + radio; off += 16) {
    if (off < 0 || off + largo >= muestras[0].length) continue;

    let suma = 0;
    for (const canal of muestras)
      for (let i = 0; i < largo; i += 4) suma += canal[i] * canal[off + i];

    if (suma > mejorPuntaje) { mejorPuntaje = suma; mejor = off; }
  }

  return mejor;
}

// El corte tiene que caer donde la onda pasa por cero y va en la misma dirección que
// al inicio del archivo. Cortar en mitad de una oscilación produce un chasquido.
function cruceCero(canal, cerca, radio) {
  const subiendoAlInicio = canal[1] >= canal[0];

  for (let d = 0; d < radio; d++) {
    for (const i of [cerca - d, cerca + d]) {
      if (i <= 0 || i + 1 >= canal.length) continue;
      const cruza = canal[i] <= 0 && canal[i + 1] > 0;
      const cruzaBajando = canal[i] >= 0 && canal[i + 1] < 0;
      if (subiendoAlInicio ? cruza : cruzaBajando) return i;
    }
  }

  return cerca;
}

function opciones(args) {
  const o = { referencia: 3, zona: 45, minimo: 40, informe: false };
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--informe') o.informe = true;
    else if (args[i].startsWith('--')) o[args[i].slice(2)] = parseFloat(args[++i]);
  }
  return o;
}

function main() {
  const args = process.argv.slice(2);
  const entrada = args[0];

  // La salida es el segundo argumento solo si no es una opción. Sin esto, pasar
  // --informe sin destino escribía un archivo llamado "--informe".
  const salida = args[1] && !args[1].startsWith("--") ? args[1] : null;
  const resto = args.slice(salida ? 2 : 1);

  if (!entrada) {
    console.log('Uso: node Tools/audio/bucle.js <archivo.wav> <salida.wav> [opciones]');
    process.exit(1);
  }

  const opt = opciones(resto);
  const a = wav.leer(entrada);
  const duracion = a.cuadros / a.frecuencia;

  console.log(`\n${path.basename(entrada)}`);
  console.log(`  ${a.frecuencia} Hz · ${a.canales} canal(es) · ${a.bits} bits · ${duracion.toFixed(2)} s\n`);

  const env = envolvente(a.muestras, a.frecuencia);
  const refLargo = Math.round(opt.referencia * HZ_ENVOLVENTE);
  const desde = Math.max(refLargo, Math.round(opt.minimo * HZ_ENVOLVENTE),
                         env.length - Math.round(opt.zona * HZ_ENVOLVENTE));
  const hasta = env.length - refLargo;

  if (hasta <= desde) {
    console.error('  La zona de búsqueda quedó vacía. Bajá --minimo o subí --zona.\n');
    process.exit(1);
  }

  const candidatos = [];
  for (let off = desde; off < hasta; off++)
    candidatos.push({ off, r: correlacion(env, 0, refLargo, off) });

  candidatos.sort((x, y) => y.r - x.r);

  console.log('  Mejores coincidencias con el comienzo:');
  console.log('   posición    parecido');
  console.log('  ─────────────────────');
  for (const c of candidatos.slice(0, 10))
    console.log(`   ${(c.off / HZ_ENVOLVENTE).toFixed(2).padStart(7)}s     ${c.r.toFixed(3)}`);

  const ganador = candidatos[0];
  console.log(`\n  Elegido: ${(ganador.off / HZ_ENVOLVENTE).toFixed(2)}s con parecido ${ganador.r.toFixed(3)}`);

  if (ganador.r < 0.5)
    console.log('  OJO: parecido bajo. Este tema puede no tener una repetición clara,\n' +
                '       y el bucle se va a notar. Probá con otra --referencia.');

  if (opt.informe) { console.log(''); return; }
  if (!salida) { console.error('\n  Falta el archivo de salida.\n'); process.exit(1); }

  const aproximado = Math.round(ganador.off / HZ_ENVOLVENTE * a.frecuencia);
  const afinado = afinar(a.muestras, aproximado, Math.round(a.frecuencia * 0.05));
  const corte = cruceCero(a.muestras[0], afinado, Math.round(a.frecuencia * 0.02));

  console.log(`  Afinado a ${(afinado / a.frecuencia).toFixed(4)}s, ` +
              `cruce por cero en ${(corte / a.frecuencia).toFixed(4)}s`);

  const recortado = a.muestras.map(c => Float32Array.from(c.subarray(0, corte)));
  wav.escribir(salida, recortado, a.frecuencia);

  console.log(`\n  escrito  ${salida}`);
  console.log(`  bucle de ${(corte / a.frecuencia).toFixed(2)}s ` +
              `(se descartaron ${(duracion - corte / a.frecuencia).toFixed(2)}s del final)\n`);
}

main();
