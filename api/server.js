const express = require('express');
const https = require('https');

const app = express();
const PORT = process.env.PORT || 3000;

// URL apuntando a tu catálogo de GitHub
const catalogUrl = 'https://raw.githubusercontent.com/Dioscarmesi/Cine.github.io/refs/heads/main/Cine/micinema_catalog.json';

let catalog = null;
let isFetching = false;

// Función para descargar el catálogo desde GitHub
function loadCatalog() {
    // Si ya hay una petición en marcha, no hacemos otra para evitar colapsar la conexión
    if (isFetching) return;
    isFetching = true;
    
    https.get(catalogUrl, (res) => {
        let rawData = '';
        res.on('data', (chunk) => { rawData += chunk; });
        res.on('end', () => {
            try {
                catalog = JSON.parse(rawData);
                console.log('✅ Catálogo JSON descargado y actualizado correctamente desde GitHub.');
            } catch (e) {
                console.error('❌ Error al procesar el JSON:', e.message);
            }
            isFetching = false;
        });
    }).on('error', (e) => {
        console.error('❌ Error de red descargando el JSON:', e.message);
        isFetching = false;
    });
}

// Cargar catálogo la primera vez al arrancar
loadCatalog();

// Ruta dinámica para redireccionar (Ejemplo: http://localhost:3000/movie_001)
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;

    // Disparar actualización en segundo plano (para tener cambios frescos sin atrasar al jugador)
    loadCatalog();

    if (!catalog || !catalog.movies) {
        return res.status(500).send("Catálogo descargándose o no disponible. Intenta de nuevo en unos segundos.");
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
