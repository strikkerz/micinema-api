# Documentación Técnica: API de MiCinema

Esta guía detalla la estructura de datos y los manejadores de comunicación (IPC) entre la interfaz (React) y el motor de Electron (Node.js).

## 1. Estructura del Catálogo (`Catalog`)

El archivo `micinema_catalog.json` es el corazón del sistema.

| Campo | Tipo | Descripción |
| :--- | :--- | :--- |
| `catalogName` | `string` | Nombre descriptivo del catálogo. |
| `version` | `number` | Versión interna para control de cambios. |
| `atlasInfo` | `object` | Configuración de las texturas (columnas, filas, etc.). |
| `movies` | `Movie[]` | Lista de contenidos (Películas y Series). |
| `series` | `Movie[]` | (Solo en JSON) Almacena los contenidos tipo serie procesados. |

---

## 2. El Objeto de Contenido (`Movie` / `Series`)

Unificado en la interfaz como un solo tipo para facilitar el manejo.

```typescript
interface Movie {
  id: string;          // Formato: movie_XXX o series_XXX
  title: string;       // Nombre visible
  year: number;        // Año de lanzamiento (4 dígitos)
  type: string;        // 'movie' o 'series'
  description: string; // Sinopsis
  rating: string;      // Clasificación (G, R, TV-MA, etc.)
  genres: string[];    // Array de géneros
  quality: string;     // 'None', 'BQ', 'LQ', 'HD'
  enabled: boolean;    // Si es visible en VRChat
  atlas: string;       // Letra del Atlas (A, B, C...)
  gridIndex: number;   // Posición en la cuadrícula (0-127)
  addedAt: string;     // ISO Timestamp de creación
  addedBy: string;     // Operador que realizó la entrada
  
  // Enlaces y Capítulos
  videoUrl?: string;   // Para Películas o Link Principal de Series
  episodes?: Array<{   // Solo para Series (Formato Unity)
    id: string;
    title: string;
    url: string;
  }>;
}
```

---

## 3. Manejadores de API (IPC Handlers)

Los comandos se invocan mediante `window.electron.invoke('nombre-comando', datos)`.

### Gestión de Archivos JSON
- **`read-json`**
  - **Recibe**: `{ path: string }`
  - **Responde**: El objeto JSON parseado.
- **`write-json`**
  - **Recibe**: `{ filePath: string, data: object }`
  - **Responde**: `boolean` (éxito).

### Procesamiento de Imágenes
- **`process-poster`**
  - **Recibe**: 
    - `posterPath`: Ruta absoluta del archivo local.
    - `atlasKey`: Letra del Atlas (ej: 'A').
    - `cellIndex`: Posición (0-127).
    - `atlasInfo`: Metadata (movieId, projectRoot).
  - **Acción**: Redimensiona el poster, lo pega en el Atlas .png y crea una copia en la carpeta `Backup/`.
- **`duplicate-backup-image`**
  - **Recibe**: `{ oldId: string, newId: string }`
  - **Acción**: Copia físicamente el `.png` del backup original al nuevo ID.
  - **Responde**: `{ success: true, path: string }`.

### Mantenimiento
- **`rebuild-atlases`**
  - **Recibe**: `{ movies: Movie[], atlasInfo: object }`
  - **Acción**: Re-escanea la carpeta `Backup/` y reconstruye todos los Atlas desde cero para optimizar espacios vacíos tras eliminaciones.

---

## 4. El Ciclo de Vida de los Datos

1. **Carga**: La App lee el JSON. Si es una Serie, traduce el array `episodes` a un diccionario de enlaces en memoria para el formulario.
2. **Edición**: El formulario modifica el objeto en memoria.
3. **Guardado**: 
   - Si es **Película**: El link principal se guarda en `videoUrl`.
   - Si es **Serie**: Se genera el array `episodes` para Unity y el link principal se guarda en `videoUrl`. El objeto `links` temporal se elimina antes de escribir el archivo para mantener el JSON limpio.
4. **Respaldo**: Cada vez que agregas contenido, la App guarda el poster original (redimensionado) en `f:/BackUp/Cinema/Backup/{ID}.png`. Este es tu "Source of Truth".

---

> [!IMPORTANT]
> **Integración con Unity/VRChat**: La propiedad `episodes` en el JSON es la que lee el script de Udon Sharp. Si cambias la estructura de los IDs en el JSON (ej: `series_001_language_Spanish_Cap_1`), asegúrate de que el script en Unity sepa cómo parsearlos.
