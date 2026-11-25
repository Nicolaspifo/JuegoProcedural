# A Punta de Machete — Documento de diseño

Proyecto final de Desarrollo de videojuegos para móviles. Juego 2D de supervivencia visto desde arriba, hecho en Unity 6 (URP 2D), con cuentas de jugador y progreso en Firebase.

## 1. Concepto

El jugador se abre paso a machete por paisajes colombianos (río, llano, pantano, páramo y selva) contra oleadas de zancudos e hipopótamos, hasta enfrentarse a **La Llorona**. La temática toma la fauna y el folclore latinoamericano.

- **Plataforma:** Android (también corre en navegador).
- **Orientación:** horizontal.
- **Sesión de juego:** niveles cortos, de 1 a 6 minutos.

## 2. Mecánicas

| Mecánica | Descripción |
|---|---|
| Movimiento | Joystick virtual en pantalla (también teclado o mando). |
| Ataque | El machete golpea solo, en arco, hacia donde se mueve el jugador. Golpear a varios enemigos de un tajo da experiencia extra. |
| Rondas | Dentro de cada nivel la dificultad sube por rondas: cada 10 enemigos eliminados (o 40 segundos) aparecen más enemigos, más rápidos y más fuertes. |
| Experiencia y cartas | Los enemigos sueltan granos de café. Al subir de nivel se elige una carta de mejora. |
| Cartas | Chigüiro (regeneración), Colibrí (ataque más rápido), Cóndor (más alcance), Hormiga Arriera (recoge de más lejos), Jaguar (+25 % de daño), Oso de Anteojos (más vida), Rana Dorada (veneno), Venado (más velocidad), Aguapanela (cura 25 %). |
| Enemigos | Zancudo (rápido, en enjambre), Hipopótamo (tanque que embiste) y la jefa La Llorona (4 fases, cantos que aturden, embestida y refuerzos). |
| Pausa | Botón táctil en la esquina superior derecha. |

## 3. Los diez niveles

Los niveles se desbloquean en orden: ganar un nivel abre el siguiente. Se pierde si el jugador muere.

| # | Nivel | Bioma | Meta y condición de victoria | Dificultad |
|---|---|---|---|---|
| 1 | Orilla del río | Río | Eliminar 20 enemigos | ×0,8 |
| 2 | El potrero | Llanura | Sobrevivir 1:15 | ×0,9 |
| 3 | Manglar | Pantano | Eliminar 40 enemigos | ×1,0 |
| 4 | Cañaduzal | Llanura | Sobrevivir 2:00 | ×1,05 |
| 5 | Noche de La Llorona | Río | Resistir 5 rondas y vencer a La Llorona | ×1,0 |
| 6 | Ciénaga | Pantano | Eliminar 70 enemigos | ×1,15 |
| 7 | Páramo | Páramo | Sobrevivir 3:00 | ×1,2 |
| 8 | Selva profunda | Selva | Eliminar 100 enemigos | ×1,3 |
| 9 | Tormenta en el llano | Llanura | Sobrevivir 4:00 | ×1,4 |
| 10 | El llanto final | Pantano | Resistir 10 rondas y vencer a La Llorona | ×1,5 |

La condición de victoria la verifica el juego en tiempo real (`LevelRuntime`): cuenta los enemigos eliminados, mide el tiempo sobrevivido o detecta la derrota de la jefa. El avance se muestra en pantalla ("Enemigos 12/40", "Sobrevive 01:10 / 02:00", "Ronda 3/5").

La dificultad de la tabla es un multiplicador sobre la vida, el daño, la velocidad y el ritmo de aparición de los enemigos.

## 4. Sistema de puntaje

Al terminar un nivel se calcula:

| Concepto | Puntos | Cuándo |
|---|---|---|
| Enemigos eliminados | 10 por enemigo | siempre |
| Victoria | 500 + 100 × número de nivel | si gana |
| Rapidez | 5 por cada segundo por debajo del tiempo de referencia | si gana, en niveles de eliminar enemigos o de jefa |
| Vida | hasta 500, según la vida restante | si gana |
| Multiplicador del nivel | × (1 + 0,1 × (nivel − 1)) | siempre |

- Perder también da puntos (los enemigos eliminados), pero sin bonos.
- De cada nivel se guarda el **mejor puntaje**. Repetir un nivel solo suma si se supera el récord.
- **Puntaje total** = suma de los mejores puntajes de los diez niveles. Es el valor del ranking.

## 5. Generación procedural

Cada nivel tiene elementos generados por código:

1. **Terreno infinito.** El suelo se genera alrededor de la cámara con varias capas de ruido Perlin: elevación (agua y playa), humedad (pantano) y formaciones rocosas, con una deformación que evita costas rectas.
2. **Bioma por nivel.** Cada nivel tiene un bioma. A partir de una semilla, el código sortea los parámetros del terreno dentro del rango de ese bioma: nivel del agua, cantidad de pantano y roca, ancho de la playa y escala del relieve. Por eso el Manglar sale pantanoso y el Páramo rocoso.
3. **Semilla por intento.** Cada partida usa una semilla nueva, así que el mapa del mismo nivel nunca se repite. La semilla queda guardada en el historial de la partida.
4. **Oleadas.** Los enemigos aparecen en posiciones aleatorias alrededor del jugador, con tipo elegido por pesos y con la dificultad escalada por ronda y por nivel.
5. **Efectos visuales.** Las texturas de partículas de golpes y ataques, y los controles táctiles, se dibujan por código, sin imágenes.

## 6. Usuarios y datos (Firebase)

- **Firebase Authentication:** registro e inicio de sesión con correo y contraseña, recuperación de contraseña y sesión que se mantiene al reabrir la app.
- **Cloud Firestore:** guarda el progreso, los puntajes y el historial.

```
users/{uid}
  displayName       texto     nombre del jugador
  totalScore        entero    suma de los mejores puntajes
  maxUnlockedLevel  entero    1 a 10
  levelsCompleted   entero
  gamesPlayed       entero
  totalKills        entero
  levels            mapa      "1".."10" → { bestScore, completed, attempts, bestTime }
  createdAt, updatedAt        fecha

users/{uid}/runs/{id}          historial, una entrada por partida
  level, won, score, kills, timeSeconds, damageTaken, seed, platform, playedAt
```

- **Ranking:** consulta de `users` ordenada por `totalScore` de mayor a menor. Muestra el top 20 y la posición del jugador.
- **Seguridad:** las reglas de Firestore permiten leer a cualquier usuario con sesión (para el ranking), pero cada jugador solo puede escribir su propio documento, y el nivel desbloqueado no puede retroceder. El correo no se guarda en Firestore.
- **Flujo al terminar un nivel:** el juego calcula el puntaje → lee el perfil del servidor → actualiza récord, total y nivel desbloqueado → guarda el perfil y agrega la partida al historial.

## 7. Pantallas

Inicio (recupera la sesión) → Login / Registro → Menú principal (usuario, puntaje total, progreso) → Selector de niveles (bloqueado, disponible, completado) → Juego (HUD con la meta) → Resultado (puntaje, récord, nivel desbloqueado) → Ranking.

## 8. Tecnología

Unity 6 con URP 2D, Input System (táctil, teclado y mando), física 2D, y Firebase Authentication y Cloud Firestore mediante su API REST. Más detalle técnico en `Backend.md`.
