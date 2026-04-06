const express = require('express');
const https = require('https');

const app = express();
const PORT = process.env.PORT || 3000;

app.set('trust proxy', true);

// URL apuntando a tu catálogo de GitHub
const catalogUrl = 'https://raw.githubusercontent.com/Dioscarmesi/Cine.github.io/refs/heads/main/Cine/micinema_catalog.json';

let catalog = null;
let isFetching = false;

// Registro de IPs autorizadas (Opcional)
const authorizedKnocks = new Map();
const KNOCK_TIMEOUT = 60 * 1000;

function loadCatalog() {
    if (isFetching) return;
    isFetching = true;
    https.get(catalogUrl, (res) => {
        let rawData = '';
        res.on('data', (chunk) => { rawData += chunk; });
        res.on('end', () => {
            try {
                catalog = JSON.parse(rawData);
                console.log('✅ Catálogo actualizado.');
            } catch (e) {
                console.error('❌ Error JSON:', e.message);
            }
            isFetching = false;
        });
    }).on('error', (e) => {
        console.error('❌ Error de red:', e.message);
        isFetching = false;
    });
}
loadCatalog();

const TROLL_VIDEO = 'https://www.youtube.com/watch?v=YcCBzPG5q3I';

// Endpoint de apoyo
app.get('/knock', (req, res) => {
    const ip = req.ip;
    authorizedKnocks.set(ip, Date.now());
    res.status(200).send("OK");
});

// Ruta principal de redirección
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;
    const userAgent = req.headers['user-agent'] || '';
    const ip = req.ip;
    const isAdmin = req.query.admin === '1';
    const ua = userAgent.toLowerCase();

    // SEGURIDAD REPARADA (User-Agent + Bypass)
    const isIpAuthorized = authorizedKnocks.get(ip) && (Date.now() - authorizedKnocks.get(ip) < KNOCK_TIMEOUT);
    
    // Identificamos clientes permitidos (Unity, VRChat, etc.)
    const isSpecialClient = ua.includes('unity') || ua.includes('vrc') || ua.includes('avpro') || ua.includes('exoplayer') || ua.includes('mpv') || ua.includes('player');
    
    // Identificamos navegadores puros de escritorio
    const isExplicitBrowser = (ua.includes('chrome') || ua.includes('edge') || ua.includes('firefox')) && !isSpecialClient;

    // Solo bloqueamos si es un navegador CLARO y NO es administrador ni ha tocado la puerta
    if (!isAdmin && !isIpAuthorized && isExplicitBrowser) {
        console.log(`🚫 BLOQUEADO: ID [${requestedId}] | UA: ${userAgent.substring(0, 40)}...`);
        return res.redirect(302, TROLL_VIDEO);
    }

    console.log(`✅ ACCESO: ID [${requestedId}] | IP: ${ip}`);

    loadCatalog();

    if (!catalog) return res.status(500).send("Cargando...");

    let movie = null;
    if (catalog.movies) movie = catalog.movies.find(m => m.id === requestedId);
    if (!movie && catalog.series) movie = catalog.series.find(s => s.id === requestedId);

    if (movie) {
        const urlToPlay = movie.videoUrl || (movie.links && movie.links.default);
        if (!urlToPlay) return res.status(404).send("Sin link.");
        return res.redirect(302, urlToPlay);
    } else {
        return res.status(404).send("No encontrado.");
    }
});

app.listen(PORT, () => {
    console.log(`🚀 Servidor en puerto ${PORT}`);
});
