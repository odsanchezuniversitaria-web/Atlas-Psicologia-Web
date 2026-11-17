# Atlas Psicología Web

Copia independiente de Atlas para Unity **6000.4.10f1**, preparada para navegador de PC, celular y WebVR. Incluye anatomía, Atlas, María, cuatro casos educativos, misiones y juegos.

**La distribución utiliza archivos estáticos de GitHub Pages. No requiere LabKey ni un Gateway adicional.** La conversación con IA utiliza el backend Atlas existente mediante LiveKit y un token temporal que entrega Nexus. Las claves de proveedores permanecen en el servidor.

## Entrega al colaborador

1. Descargar `Atlas-Psicologia-Web.zip` de la release `web-v0.1.0`.
2. Descomprimirlo: `index.html`, `Build/`, `StreamingAssets/` y los archivos de interfaz deben conservarse juntos.
3. Publicarlo desde la raíz de un sitio HTTPS. Los recursos usan rutas relativas y admiten la subcarpeta de un repositorio de GitHub Pages.
4. Para usar el workflow preparado, habilitar **Settings → Pages → GitHub Actions** y ejecutar **Publicar build en GitHub Pages** con la etiqueta de la release.
5. Integrar la sesión de Nexus siguiendo [INTEGRACION-NEXUS.txt](Docs/INTEGRACION-NEXUS.txt).

El repositorio de entrega permanece **privado**. La publicación de Pages la realiza el colaborador en el repositorio y cuenta que corresponda. GitHub Free permite Pages en repositorios públicos; Pages desde un repositorio privado requiere un plan compatible. [Documentación de GitHub](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages).

## Estado de la IA

El transporte web conserva LiveKit, los comandos de Atlas, el contexto de la pieza y las respuestas de la paciente. Mantiene la voz seleccionada en el backend.

Para conversar desde el sitio publicado, Nexus debe entregar un JWT temporal válido y el backend debe permitir el origen de Pages en CORS. **La IA no se habilita colocando claves privadas en este repositorio.** Sin sesión se puede explorar y guardar avance local, pero no se presenta una conexión de voz inexistente como funcional.

## Proyecto Unity

El archivo anatómico grande y las bibliotecas nativas del SDK utilizan Git LFS:

```powershell
git lfs install
git clone https://github.com/odsanchezuniversitaria-web/Atlas-Psicologia-Web.git
cd Atlas-Psicologia-Web
git lfs pull
```

Abrir con Unity 6000.4.10f1 y el módulo WebGL instalado. Las escenas son:

- `Assets/@Xyla/Scenes/AtlasWebInicio.unity`
- `Assets/@Xyla/Scenes/AtlasWebLab.unity`

En Hierarchy, `AtlasWeb` contiene las referencias editables de cámara, jugador, controles y rayo. La paciente y el consultorio conservan sus objetos de escena. La interfaz de PC/celular está en `Assets/WebGLTemplates/AtlasNexus`.

Para compilar, usar **Atlas Web → Compilar Web PC, Celular y WebVR**. El destino es `Builds/AtlasPages/`. Las carpetas de caché, credenciales, servidores antiguos y builds incompletos quedan fuera de Git.

LiveKit nativo queda excluido del player WebGL; allí se utiliza su SDK JavaScript. Los inicializadores nativos de MRUK también se desactivan en navegador. Los cambios se describen en los archivos `ATLAS-WEBGL-PATCHES.txt` de esos paquetes.

## Comprobaciones locales

```powershell
cd WebBuildTools
npm ci
node --test client.test.cjs voice.test.cjs
node verify-build.cjs ../Builds/AtlasPages
node serve.cjs
```

Después de verificar el juego en navegador, `WebBuildTools/prepare-release.ps1` genera el ZIP, el manifiesto y su checksum en `ReleaseArtifacts/`.

Node recomendado: **22.22.0 o posterior**. La vista local abre en `http://127.0.0.1:8860`.

Los ensayos automatizados usan dobles de LiveKit, no consumen proveedores de IA y no equivalen a una prueba física del visor. Ver [VALIDACION-WEB.json](Docs/VALIDACION-WEB.json) para el alcance verificado de la entrega.

## Controles

- **PC:** WASD, botón derecho para mirar, arrastrar piezas con botón izquierdo, rueda para escalar y espacio para hablar.
- **Celular:** joystick y zona táctil de mirada/arrastre; mantener Hablar y soltar al terminar.
- **WebVR:** botón Entrar en VR; palancas para caminar/girar, agarre para piezas, gatillo para paneles y A/X para hablar. El soporte depende del navegador del visor.

## Atribuciones

Se conservan los avisos de licencia de los modelos, materiales, SDK de Meta, LiveKit y WebXR incluidos en sus directorios. Los ejemplos opcionales `Samples~` de los SDK se excluyen de la entrega, incluidas sus configuraciones de acceso de demostración; no son necesarios para Atlas. Esta entrega privada no sustituye sus licencias ni concede permisos adicionales sobre esos recursos.
