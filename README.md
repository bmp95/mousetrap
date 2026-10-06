# CentrarRaton

¿Has perdido el puntero entre varias pantallas? **Mantén pulsados los dos botones del ratón durante 3 segundos** y el puntero salta al centro de la pantalla que elijas, marcado con un disco naranja para que lo veas al instante.

> **English:** a tiny Windows tray utility. Hold both mouse buttons for 3 seconds and the pointer jumps to the middle of a screen of your choice, highlighted with an orange disc. The menu is shown in English on non-Spanish systems.

- Un solo `.exe` de pocos KB. No instala nada y no necesita permisos de administrador.
- Funciona con varias pantallas, también si tienen distinto zoom o están en vertical.
- Al soltar los botones no se hace clic ni se abre ningún menú contextual en lo que hubiera debajo.

## Uso

1. Ejecuta `CentrarRaton.exe`. Aparece un círculo naranja en la bandeja del sistema, junto al reloj.
2. Mantén pulsados el botón izquierdo y el derecho a la vez durante 3 segundos.
3. El puntero aparece en el centro de la pantalla principal.

Con el botón derecho sobre el icono de la bandeja puedes:

| Opción | Qué hace |
| --- | --- |
| La pantalla principal | Siempre al centro de la pantalla principal (por defecto). |
| La pantalla donde ya esté el puntero | Lo centra sin cambiarlo de pantalla. |
| Pantalla 1, 2, 3… | Siempre al centro de esa pantalla. Si se desconecta, usa la principal. |
| Iniciar con Windows | Arranca la aplicación al iniciar sesión. |
| Salir | Cierra la aplicación. |

## Configuración

Lo que elijas en el menú se guarda en `%APPDATA%\CentrarRaton\config.ini`. Ahí también puedes cambiar el tiempo de pulsación; reinicia la aplicación después de editarlo.

```ini
# primary | cursor | nombre de la pantalla, por ejemplo \\.\DISPLAY2
target=primary
# milisegundos que hay que mantener los dos botones (mínimo 500)
hold_ms=3000
```

## Compilar

Solo hace falta Windows 10 u 11: se compila con el compilador de C# que ya viene con el sistema (.NET Framework 4).

```bat
build.cmd
```

El resultado queda en `dist\CentrarRaton.exe`.

## Tests

```bat
test.cmd
```

ejecuta los tests de la lógica (detección de la pulsación, configuración y elección de pantalla).

```bat
test.cmd e2e
```

añade una prueba de extremo a extremo contra el `.exe` real. Pulsa los botones con entrada sintética sobre una ventana propia y comprueba dónde acaba el puntero, así que **toma el control del ratón durante unos 20 segundos**. Cierra antes la aplicación si la tienes abierta.

## Cómo funciona

- Un temporizador consulta 20 veces por segundo el estado de los dos botones (`GetAsyncKeyState`). No instala ganchos de ratón ni de teclado.
- Cuando llevan pulsados juntos el tiempo configurado, avisa a la ventana que tenía la pulsación a medias para que la cancele (`WM_CANCELMODE`) y mueve el puntero (`SetCursorPos`).
- El disco naranja aparece justo debajo del puntero y recibe él las sueltas de los botones; por eso no llegan a la ventana que haya debajo.

## Limitaciones

- No actúa mientras la ventana activa pertenece a un programa abierto como administrador (el Administrador de tareas, por ejemplo): Windows no deja que un programa normal vea ahí el estado del ratón.
- En juegos que usan los dos botones a la vez durante varios segundos el puntero también saltará. Sal de la aplicación desde la bandeja mientras juegas.
- El ejecutable no está firmado, así que algunos antivirus lo analizan o retienen la primera vez que se ejecuta.

## Licencia

© 2026 Bernabé Muñoz Peñas. Software libre bajo la [GNU GPL v3.0](LICENSE): puedes usarlo, modificarlo y redistribuirlo; si distribuyes una versión modificada, tiene que ir con su código y bajo esta misma licencia.
