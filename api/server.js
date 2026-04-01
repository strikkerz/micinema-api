const express = require('express');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

// Ruta al archivo JSON (ahora en la misma carpeta que el servidor)
const catalogPath = path.join(__dirname, 'micinema_catalog_atlas_only_test.json');

let catalog = null;

// Función para cargar o recargar el catálogo
function loadCatalog() {
    try {
        const rawData = fs.readFileSync(catalogPath, 'utf8');
        catalog = JSON.parse(rawData);
        console.log('✅ Catálogo JSON cargado correctamente.');
    } catch (error) {
        console.error('❌ Error al cargar el catálogo JSON:', error.message);
    }
}

// Cargar catálogo al inicio
loadCatalog();

// Ruta dinámica para redireccionar (Ejemplo: http://localhost:3000/movie_001)
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;

    // Recargar catálogo en cada petición (opcional, útil para actualizarlo sin reiniciar el servidor)
    loadCatalog();

    if (!catalog || !catalog.movies) {
        return res.status(500).send("Catálogo no disponible.");
    }

    // Buscar la película en el catálogo
    const movie = catalog.movies.find(m => m.id === requestedId);

    if (movie) {
        console.log(`➡️ Redireccionando [${requestedId}] a: ${movie.videoUrl}`);
        // Retornar un HTTP 302 Found (Redirección temporal)
        return res.redirect(302, movie.videoUrl);
    } else {
        console.log(`⚠️ ID no encontrado: [${requestedId}]`);
        return res.status(404).send("Película no encontrada.");
    }
});

// Iniciar servidor
app.listen(PORT, () => {
    console.log('====================================');
    console.log(`🚀 API intermedio corriendo en:`);
    console.log(`http://localhost:${PORT}`);
    console.log(`Prueba: http://localhost:${PORT}/movie_001`);
    console.log('====================================');
});
