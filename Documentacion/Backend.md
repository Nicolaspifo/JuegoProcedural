# Backend: Firebase, niveles y puntaje

Este documento explica cómo está armado el backend del juego, cómo configurarlo y cómo lo usan las pantallas.

## 1. Arquitectura

```
Pantallas (UI)  ──►  Backend.Service  (IBackendService)
                          │
            ┌─────────────┴─────────────┐
            ▼                           ▼
      MockBackend                FirebaseBackend
  (datos falsos locales,      (Firebase Auth + Firestore
   para diseñar pantallas)        por API REST)

Escena de juego ──► LevelRuntime ──► LevelScoring ──► Backend.Service.SubmitLevelResult
                        │
                        └─► LevelCatalog (10 niveles) + terreno procedural (InfiniteTilemapFloor)
```

- **Las pantallas nunca hablan con Firebase directamente**: solo con `Backend.Service`.
- Firebase se usa por **API REST** (`UnityWebRequest`). No hace falta importar el SDK nativo y funciona igual en Android y en Web.
- El modo **Mock** se activa con la casilla *Use Mock* de `Assets/_Project/Resources/FirebaseSettings.asset`. Así tu compañero puede diseñar y probar las pantallas sin internet.

### Archivos

| Carpeta | Contenido |
|---|---|
| `Scripts/Backend/Contract/` | `IBackendService` (el contrato), modelos (`UserProfile`, `RankingEntry`...), `ProgressRules`, `BackendValidation` |
| `Scripts/Backend/Firebase/` | `FirebaseBackend`, clientes REST de Auth y Firestore, traducción de errores, sesión guardada |
| `Scripts/Backend/Mock/` | `MockBackend` |
| `Scripts/Backend/Util/` | `MiniJson` y `HttpClientUnity` (async/await sobre UnityWebRequest) |
| `Scripts/Backend/Testing/` | `BackendTestPanel`, panel de pruebas para las evidencias |
| `Scripts/Levels/` | `LevelCatalog`, `LevelSession`, `LevelRuntime`, `LevelScoring`, `LevelModifiers` y la pantalla de resultado de respaldo |
| `Resources/FirebaseSettings.asset` | API key, ID del proyecto y modo Mock |
| `Firebase/firestore.rules` (raíz del repo) | Reglas de seguridad de Firestore |

## 2. Configurar Firebase (una sola vez)

1. Entra a <https://console.firebase.google.com> y crea un proyecto (Google Analytics es opcional).
2. **Authentication** → *Comenzar* → pestaña *Método de inicio de sesión* → habilita **Correo electrónico/contraseña**.
3. **Firestore Database** → *Crear base de datos* → modo **producción** → elige una ubicación (por ejemplo `southamerica-east1`).
4. En **Firestore → Reglas**, pega el contenido de `Firebase/firestore.rules` y pulsa **Publicar**.
5. En **⚙ Configuración del proyecto → General**:
   - Copia el **ID del proyecto**.
   - En *Tus apps* agrega una **app web** (ícono `</>`), sin hosting, y copia la **Clave de API web** (`apiKey`).
6. En Unity abre `Assets/_Project/Resources/FirebaseSettings.asset`:
   - Pega **Web Api Key** y **Project Id**.
   - **Desmarca *Use Mock***.

> La Web API Key no es secreta: solo identifica el proyecto. La seguridad la dan las reglas de Firestore, que dejan a cada usuario escribir únicamente su propio documento.

El ranking necesita un índice de un solo campo sobre `totalScore`. Firestore lo crea automáticamente, así que no hay que hacer nada.

## 3. Estructura de datos (Firestore)

```
users/{uid}                                  ← un documento por jugador
  displayName       string    "Machetero"
  totalScore        int       suma de los MEJORES puntajes de cada nivel
  maxUnlockedLevel  int       1..10
  levelsCompleted   int       0..10
  gamesPlayed       int
  totalKills        int
  levels            map
    "1": { bestScore: int, completed: bool, attempts: int, bestTime: double }
    "2": { ... }
  createdAt         timestamp
  updatedAt         timestamp

users/{uid}/runs/{autoId}                    ← historial: una entrada por partida
  level, won, score, kills, timeSeconds, damageTaken, seed, platform, playedAt
```

- El **correo no se guarda en Firestore**: el ranking lo leen todos los usuarios logueados y no debe exponer correos.
- **Ranking**: consulta `users` ordenada por `totalScore` de mayor a menor (top 20). La posición propia, si no está en el top, se calcula contando cuántos jugadores tienen más puntos (consulta de agregación `count`).
- **Sesión persistente**: se guarda solo el *refresh token* en `PlayerPrefs`. Al abrir la app, `TryRestoreSession()` lo cambia por un token nuevo y carga el perfil. La contraseña nunca se guarda.

## 4. Reglas de progresión y puntaje

**Progresión** (`ProgressRules`, la usan igual el Mock y Firebase):
- Ganar el último nivel desbloqueado desbloquea el siguiente.
- De cada nivel solo cuenta el **mejor intento**: repetir un nivel no infla el ranking.
- Puntaje total = suma de los mejores puntajes de los 10 niveles.

**Puntaje de un nivel** (`LevelScoring`):

| Concepto | Puntos | Cuándo |
|---|---|---|
| Kills | 10 por enemigo | siempre |
| Victoria | 500 + 100 × número de nivel | si gana |
| Rapidez | (tiempo de referencia − tiempo) × 5 | si gana, en metas de kills o jefe |
| Vida | % de vida restante × 500 | si gana |
| **Multiplicador** | × (1 + 0,1 × (nivel − 1)) | siempre |

## 5. Los 10 niveles

Están definidos en `LevelCatalog.cs`.

| # | Nombre | Bioma | Meta (condición de victoria) | Dificultad |
|---|---|---|---|---|
| 1 | Orilla del río | Río | Matar 20 enemigos | ×0,8 |
| 2 | El potrero | Llanura | Sobrevivir 1:15 | ×0,9 |
| 3 | Manglar | Pantano | Matar 40 enemigos | ×1,0 |
| 4 | Cañaduzal | Llanura | Sobrevivir 2:00 | ×1,05 |
| 5 | Noche de La Llorona | Río | Resistir 5 rondas y vencer a La Llorona | ×1,0 |
| 6 | Ciénaga | Pantano | Matar 70 enemigos | ×1,15 |
| 7 | Páramo | Páramo | Sobrevivir 3:00 | ×1,2 |
| 8 | Selva profunda | Selva | Matar 100 enemigos | ×1,3 |
| 9 | Tormenta en el llano | Llanura | Sobrevivir 4:00 | ×1,4 |
| 10 | El llanto final | Pantano | Resistir 10 rondas y vencer a La Llorona | ×1,5 |

**Derrota:** el jugador muere.

**Generación procedural en cada nivel:**
1. **Terreno**: `InfiniteTilemapFloor` genera el suelo infinito con ruido Perlin (elevación, humedad y roca). Cada nivel tiene un bioma, y `LevelCatalog.GenerateBiome(tema, semilla)` sortea con la semilla los parámetros dentro del rango de ese bioma: nivel del agua, pantano, roca, ancho de playa y escala.
2. **Semilla por intento**: `LevelSession` crea una semilla nueva en cada partida, así que el mapa nunca es igual. Con `RandomizeEachAttempt = false` cada nivel tiene siempre el mismo mapa.
3. **Dificultad**: `LevelModifiers` multiplica vida, daño, velocidad, ritmo de aparición y máximo de enemigos sobre la curva por rondas de `DifficultyDirector`.
4. **Efectos**: `ProceduralSprites` genera por código las texturas de las partículas.

La semilla de cada partida queda guardada en el historial (`runs`).

## 6. Cómo lo usan las pantallas (para el compañero de UI)

Todas las llamadas son `async`. Mientras esperan hay que mostrar "Cargando…" y desactivar los botones. Si `r.Ok` es falso, se muestra `r.Message`, que ya viene en español. Si `r.CanRetry` es verdadero, se ofrece un botón **Reintentar**.

| Pantalla | Llamada | Qué hacer con el resultado |
|---|---|---|
| Splash | `await Backend.Service.TryRestoreSession()` | `Ok` → menú · `CanRetry` → error + Reintentar · otro → login |
| Registro | `BackendValidation.ValidateRegister(nombre, correo, pass, confirmar)` y después `await Backend.Service.Register(correo, pass, nombre)` | `Ok` → menú |
| Login | `await Backend.Service.Login(correo, pass)` | `Ok` → menú |
| Olvidé contraseña | `await Backend.Service.SendPasswordReset(correo)` | mostrar `Message` |
| Menú | `await Backend.Service.LoadProfile()` → `UserProfile` | `displayName`, `totalScore`, `maxUnlockedLevel`, `levelsCompleted`, `Progress01` |
| Cerrar sesión | `Backend.Service.Logout()` | ir al login |
| Selector de niveles | `LevelCatalog.All` (nombre, `GoalText`, `description`), `LevelSession.CanPlay(n)`, `CurrentUser.IsCompleted(n)`, `CurrentUser.BestScore(n)` | `LevelSession.Play(n)` |
| HUD | `LevelRuntime.Instance.GoalProgressText` / `GoalProgress01` | poner `LevelRuntime.ShowFallbackHud = false` |
| Resultado | Eventos `LevelRuntime.LevelFinished` (puntaje y desglose), `ResultSaved` (récord, desbloqueo, nuevo total) y `ResultSaveFailed` (botón → `LevelRuntime.Instance.RetrySave()`) | `LevelSession.PlayNext()`, `Retry()`, `GoToMenu()` |
| Ranking | `await Backend.Service.GetRanking(20)` → `entries` + `me` | resaltar `isCurrentUser`; si `!meInTop`, mostrar `me` abajo |

Ejemplo:

```csharp
async void OnLoginClicked()
{
    loginButton.interactable = false;
    loadingText.SetActive(true);

    Result r = await Backend.Service.Login(emailInput.text, passwordInput.text);

    loadingText.SetActive(false);
    loginButton.interactable = true;

    if (r.Ok) SceneManager.LoadScene("MenuPrincipal");
    else errorText.text = r.Message;
}
```

Mientras no exista la pantalla de resultado final, `LevelRuntime` muestra una de respaldo (`LevelResultFallbackUI`). Desaparece sola en cuanto alguna pantalla se suscribe a `LevelRuntime.LevelFinished`.

## 7. Probar y sacar evidencias

1. En la escena `MenuPrincipal`, crea un objeto vacío y agrégale el componente **BackendTestPanel**. Aparece un botón "Backend" arriba a la izquierda.
2. Con el **Mock** se prueba todo sin internet. Con **Firebase** se ve en la consola de Firebase.
3. Evidencias sugeridas (capturas o grabación):
   - **Autenticación**: registrar 2 o 3 usuarios → *Authentication → Usuarios* muestra las cuentas.
   - **Almacenamiento**: jugar un nivel → *Firestore → users/{uid}* muestra `totalScore`, `levels` y el historial en `runs`.
   - **Progresión**: ganar el nivel 1 → `maxUnlockedLevel` pasa a 2 y el panel deja "Jugar nivel 2".
   - **Persistencia**: cerrar la app, volver a abrirla → `Restaurar sesión` carga el mismo progreso. Cerrar sesión, entrar con otro usuario y ver su propio progreso.
   - **Ranking**: varios usuarios con puntajes distintos → `Ranking` los ordena.
   - **Consola de Unity**: cada petición se registra con `[HTTP]`, y cada nivel con su semilla y bioma (`[Nivel N] ...`).
4. **Quitar `BackendTestPanel`** antes de la entrega final.

## 8. Problemas comunes

| Mensaje | Causa |
|---|---|
| "La Web API Key de Firebase no es válida" | API key mal copiada en `FirebaseSettings` |
| "El inicio de sesión con correo no está activado" | Falta el paso 2 de la configuración |
| "La base de datos Firestore no está creada" | Falta el paso 3 |
| "Sin permiso para esa operación" | No se publicaron las reglas (paso 4), o el nombre tiene menos de 3 o más de 16 caracteres |
| "Sin conexión" | Sin internet. En Android, revisar *Player Settings → Internet Access = Require* |

## 9. Pantallas conectadas

Las escenas que diseñó el equipo se conectan por código: al cargar cada escena, `ScreenRouter` le agrega su controlador, y este busca los objetos **por nombre**. Si se renombra un botón en Unity, hay que cambiar el nombre en el script correspondiente.

| Escena | Script | Objetos que usa |
|---|---|---|
| `MenuPrincipal` (inicio) | `MainMenuScreen` | `PlayButton` (Jugar), `PlayButton (1)` (Instrucciones), `Salir`. Crea los botones Ranking y Cerrar sesión y el panel del jugador (nombre, puntaje total, progreso). Al abrir recupera la sesión guardada. |
| `Inicio Secion` | `LoginScreen` | `Datos` (campos de correo y contraseña encima de las cajas dibujadas), `Entrar`, `Crear cuenta`, `Volver`. Agrega "¿Olvidaste tu contraseña?" y el botón Ver contraseña. |
| `Crear cuenta` | `RegisterScreen` | `Datos` (correo, contraseña y nombre), `Registrarme`, `Ya tengo cuenta`. |
| `Niveles` | `LevelSelectScreen` | `N1`…`N10` (bloqueado con calavera, disponible o completado en dorado), `Jugar nivel seleccionado`, `Instrucciones`, `Volver`. Muestra la meta y el récord del nivel elegido. |
| `Ranking` | `RankingScreen` | Columnas `Titulo (2)`, `Titulo (3)` y `Titulo (4)` (top 10), `Titulo (5)` ("Tu posición"), `Entrar` (Volver) y `PlayButton` (Actualizar). |
| `Instrucciones` | `InstructionsScreen` | Se arma por código mientras la escena siga vacía. Si se diseña con un botón `Volver`, se usa ese diseño. |
| `Principal` (juego) | `LevelHudScreen` | `Temporizador` (tiempo) y `Temporizador (1)` (enemigos). Agrega el nombre del nivel, la meta con barra de avance y la pantalla de resultado. |

Los avisos (`UIAviso`) usan las imágenes del diseño (Cargando, Error, Sin conexión, Éxito y Sincronizar), copiadas a `Resources/UI/` junto con las fuentes y la tabla de madera para poder usarlas en cualquier escena.

**Orden del build:** `MenuPrincipal`, `Inicio Secion`, `Crear cuenta`, `Niveles`, `Ranking`, `Instrucciones` y `Principal`.
