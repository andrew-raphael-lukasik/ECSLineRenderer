# ECSLineRenderer
Line renderer for Unity.Entities tech stack

# requirements
- for built-in shader:
```
"com.unity.render-pipelines.universal": "9.0.0-preview.55"
```
note: you can override dafault shader/material with your own.

# samples
- mesh wireframe (runtime)
<img src="https://i.imgur.com/NCC71mD.gif" height="200">

- drawing mesh bounding boxes (runtime)
<img src="https://i.imgur.com/J1mzvSbl.jpg" height="200">

# installation Unity 2020.x
Add this line in `manifest.json` / `dependencies`:
```
"com.andrewraphaellukasik.ecslinerenderer": "https://github.com/andrew-raphael-lukasik/ECSLineRenderer.git#upm",
```

Or via `Package Manager` / `Add package from git URL`:
```
https://github.com/andrew-raphael-lukasik/ECSLineRenderer.git#upm
```
