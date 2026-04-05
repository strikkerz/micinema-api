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

// === SEGURIDAD: Token y User-Agent ===
const SECRET_TOKEN = process.env.MAP_TOKEN || 'M1C1N3M4_S3CR3T_K3Y';
const TROLL_VIDEO = 'https://www.youtube.com/watch?v=YcCBzPG5q3I';

function isValidVRChatClient(userAgent) {
    if (!userAgent) return false;
    const ua = userAgent.toLowerCase();
    
    // Identificadores comunes de VRChat, Unity y reproductores de medios nativos
    const allowed = ['unity', 'avpro', 'vrc', 'exoplayer', 'wmplayer', 'wmf', 'nsplayer', 'curl', 'yt-dlp'];
    
    return allowed.some(keyword => ua.includes(keyword));
}

// Ruta dinámica para redireccionar (Ejemplo: http://localhost:3000/movie_001)
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;
    const token = req.query.key;
    const userAgent = req.headers['user-agent'] || '';

    // === Verificar seguridad por partes para mejores logs ===
    
    // 1. Verificar Token
    if (token !== SECRET_TOKEN) {
        console.log(`🚫 Bloqueado por KEY incorrecta: ID [${requestedId}] | Key recibida: ${token}`);
        return res.redirect(302, TROLL_VIDEO);
    }

    // 2. Verificar Reproductor (User-Agent)
    if (!isValidVRChatClient(userAgent)) {
        console.log(`🚫 Bloqueado por NAVEGADOR (UA no válido): ID [${requestedId}] | UA: ${userAgent}`);
        return res.redirect(302, TROLL_VIDEO);
    }

    // Disparar actualización en segundo plano (para tener cambios frescos sin atrasar al jugador)
    loadCatalog();

    if (!catalog || !catalog.movies) {
        return res.status(500).send("Catálogo descargándose o no disponible. Intenta de nuevo en unos segundos.");
    }

    // Buscar la película en el catálogo
    const movie = catalog.movies.find(m => m.id === requestedId);

    if (movie) {
        // Encontrar la URL correcta (soporta el formato antiguo y el nuevo con "links")
        const urlToPlay = movie.videoUrl || (movie.links && movie.links.default);
        
        if (!urlToPlay || typeof urlToPlay !== 'string' || urlToPlay.trim() === '') {
            console.log(`⚠️ Película sin enlace configurado: [${requestedId}]`);
            return res.status(404).send("La película existe en tu catálogo, pero no tiene un link de YouTube guardado.");
        }

        console.log(`➡️ Redireccionando [${requestedId}] a: ${urlToPlay}`);
        // Retornar un HTTP 302 Found (Redirección temporal)
        return res.redirect(302, urlToPlay);
    } else {
        console.log(`⚠️ ID no encontrado: [${requestedId}]`);
        return res.status(404).send("Película no encontrada (verifica que el ID sea correcto).");
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
