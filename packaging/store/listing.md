# Microsoft Store listing

What goes into each field of the submission in Partner Center, in English and in Spanish. The images are in `en/` and `es/`, in the order they should appear.

## Once, for both languages

| Field | Value |
|---|---|
| Price | Free |
| Category | Utilities & tools |
| Privacy policy URL | https://github.com/bmp95/mousetrap#-code-signing-policy |
| Website | https://github.com/bmp95/mousetrap |
| Support contact | https://github.com/bmp95/mousetrap/issues |
| Age rating | Answer "No" to everything in the questionnaire: the app shows no content of its own |

### Why the app needs `runFullTrust`

```text
Mousetrap is a notification-area utility. It reads the global state of the mouse buttons (GetAsyncKeyState), moves the pointer (SetCursorPos), registers a system-wide hot key (RegisterHotKey) and shows a notification-area icon. None of that is available to a sandboxed app. It makes no network connections and writes only its own settings file.
```

### Notes for certification

```text
Mousetrap has no main window. After launch it shows an icon in the notification area, an orange box propped up on a stick; it may be under the ^ arrow of hidden icons.

To test: hold the left and right mouse buttons together for 3 seconds. The pointer jumps to the centre of the main screen and an orange disc appears there for a moment. Left-click the notification-area icon to open Settings; right-click it for the menu.

The package declares a startup task so that the utility is there after sign-in. The user can switch it off in Settings > Apps > Startup, and the app's menu has an entry that opens that page.

It makes no network connections and collects no data. It is open source: https://github.com/bmp95/mousetrap
```

## English (United States)

### Description

```text
Lost your pointer among several screens? Hold both mouse buttons for a moment and Mousetrap brings it to the middle of the screen you choose, marked with an orange disc so your eye finds it at once.

• Call the pointer by holding both mouse buttons, or with a keyboard shortcut you record by pressing it. Use either, or both.
• Send it to the main screen, to the screen it is already on, or to a specific one.
• Choose how long to hold, from half a second to four seconds.
• Letting go of the buttons clicks nothing and opens no menu on whatever was underneath.
• Works with screens at different zoom levels, in portrait, or placed above one another.
• Lives quietly in the notification area and starts with Windows. You can switch that off in Settings > Apps > Startup.
• In English or Spanish.

Mousetrap is small and private: it installs no mouse or keyboard hooks, makes no network connections and collects nothing. It is free, open-source software under the GPL-3.0 licence; the source is at github.com/bmp95/mousetrap.
```

### Short description

```text
Hold both mouse buttons and your lost pointer jumps to the centre of the screen you choose.
```

### Product features

```text
Hold both mouse buttons, or your own keyboard shortcut, to call the pointer
Jumps to the centre of the main screen, the current screen or one you pick
An orange disc marks the spot and takes the button releases: no stray clicks
Adjustable hold time, from 0.5 to 4 seconds
Handles mixed zoom levels, portrait screens and stacked layouts
No hooks, no network connections, nothing collected
In English and Spanish
```

### Search terms

```text
mouse finder
find cursor
lost pointer
multi monitor
multiple screens
mouse
cursor
```

### Screenshots

`en/1-jump-en.png`, `en/2-disc-en.png`, `en/3-settings-en.png`

## Español (España)

### Descripción

```text
¿Has perdido el puntero entre varias pantallas? Mantén pulsados los dos botones del ratón un momento y Mousetrap lo trae al centro de la pantalla que elijas, marcado con un disco naranja para que lo veas al instante.

• Llama al puntero manteniendo los dos botones del ratón, o con un atajo de teclado que grabas pulsándolo. Usa uno, o los dos.
• Envíalo a la pantalla principal, a la pantalla donde ya esté o a una concreta.
• Elige cuánto hay que mantener pulsado, de medio segundo a cuatro segundos.
• Al soltar los botones no se pulsa nada ni se abre ningún menú en lo que hubiera debajo.
• Funciona con pantallas de distinto zoom, en vertical o colocadas una encima de otra.
• Vive discretamente en la bandeja del sistema y arranca con Windows. Puedes desactivarlo en Configuración > Aplicaciones > Inicio.
• En español o en inglés.

Mousetrap es pequeño y privado: no instala ganchos de ratón ni de teclado, no se conecta a nada y no recoge ningún dato. Es software libre y gratuito bajo licencia GPL-3.0; el código está en github.com/bmp95/mousetrap.
```

### Descripción breve

```text
Mantén los dos botones del ratón y el puntero perdido salta al centro de la pantalla que elijas.
```

### Características del producto

```text
Mantén los dos botones del ratón, o tu propio atajo de teclado, para llamar al puntero
Salta al centro de la pantalla principal, de la actual o de la que elijas
Un disco naranja marca el sitio y recibe las sueltas de los botones: sin clics fantasma
Tiempo de pulsación ajustable, de 0,5 a 4 segundos
Admite pantallas con distinto zoom, en vertical y apiladas
Sin ganchos, sin conexiones de red, sin recoger datos
En español e inglés
```

### Términos de búsqueda

```text
buscar raton
encontrar cursor
puntero perdido
varias pantallas
multi monitor
raton
cursor
```

### Capturas de pantalla

`es/1-jump-es.png`, `es/2-disc-es.png`, `es/3-settings-es.png`
