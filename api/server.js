const express = require('express');
const https = require('https');

const app = express();
const PORT = process.env.PORT || 3000;

app.set('trust proxy', true);

// URLs de GitHub
const catalogUrl = 'https://raw.githubusercontent.com/Dioscarmesi/Cine.github.io/refs/heads/main/Cine/micinema_catalog.json';
const keyUrl = 'https://raw.githubusercontent.com/strikkerz/micinema-api/main/Key.json';

let catalog = null;
let masterKey = null;
let isFetching = false;

// Registro de IPs autorizadas
const authorizedKnocks = new Map();
const KNOCK_TIMEOUT = 10 * 60 * 1000; // El permiso dura 10 minutos por sesión

function loadData() {
    if (isFetching) return;
    isFetching = true;

    // Cargar Catálogo
    https.get(catalogUrl, (res) => {
        let data = '';
        res.on('data', c => data += c);
        res.on('end', () => { try { catalog = JSON.parse(data); console.log('✅ Catálogo cargado.'); } catch(e){} });
    });

    // Cargar Llave Maestra
    https.get(keyUrl, (res) => {
        let data = '';
        res.on('data', c => data += c);
        res.on('end', () => {
            try { 
                const json = JSON.parse(data);
                masterKey = json.api_key;
                console.log('🛡️ Llave Maestra cargada correctamente.');
            } catch(e){ console.error('❌ Error cargando llave:', e.message); }
        });
    });

    isFetching = false;
}
loadData();

const TROLL_VIDEO = 'https://www.youtube.com/watch?v=YcCBzPG5q3I';

// === ENDPOINT DE "TOQUE" (CREDENCIAL) ===
app.get('/knock', (req, res) => {
    const ip = req.ip;
    const clientKey = req.query.key;

    if (!masterKey) return res.status(503).send("Servidor sincronizando llave...");

    if (clientKey === masterKey) {
        authorizedKnocks.set(ip, Date.now());
        console.log(`🔑 CREDENCIAL OK: IP [${ip}] autorizada.`);
        return res.status(200).send("ACCESO_CONCEDIDO");
    } else {
        console.log(`👮 INTENTO FALLIDO: IP [${ip}] con llave incorrecta.`);
        return res.status(401).send("LLAVE_INVALIDA");
    }
});

// Ruta principal de redirección
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;
    const ip = req.ip;
    const isAdmin = req.query.admin === '1';

    // VERIFICACIÓN OBLIGATORIA DE CREDENCIAL
    const knockTime = authorizedKnocks.get(ip);
    const isAuthorized = knockTime && (Date.now() - knockTime < KNOCK_TIMEOUT);

    if (!isAuthorized && !isAdmin) {
        console.log(`🚫 BLOQUEADO: IP [${ip}] intentó ver [${requestedId}] sin credencial.`);
        return res.redirect(302, TROLL_VIDEO);
    }

    console.log(`✅ DISFRUTANDO: ID [${requestedId}] | IP: ${ip}`);
    
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
    console.log(`🚀 Servidor Seguro Protegido por Key.json activo.`);
});
