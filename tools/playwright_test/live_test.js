// Live WebGL playtest harness — opens the deployed build in a real Chromium,
// captures all browser console output + errors, clicks canvas positions that
// correspond to the main-menu buttons, and takes screenshots at each step.
//
// Unity WebGL renders everything to a single <canvas>, so we can't query DOM
// buttons. We interact by computing canvas-relative pixel coordinates and
// dispatching real mouse events to the canvas element.
//
// Usage:
//   node live_test.js [url]
//   (defaults to https://taeshin11.github.io/LittleLordMajesty/)

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const URL = process.argv[2] || 'https://taeshin11.github.io/LittleLordMajesty/';
const OUT_DIR = path.join(__dirname, 'screenshots');
if (!fs.existsSync(OUT_DIR)) fs.mkdirSync(OUT_DIR, { recursive: true });

// Unity reports its canvas at a fixed reference resolution (1080x1920 per
// CanvasScaler). In the Unity web template, the canvas CSS size depends on
// the browser viewport. We'll query the live canvas rect when dispatching
// clicks.
const VIEWPORT = { width: 1280, height: 960 };

(async () => {
    console.log(`[LiveTest] Launching Chromium against ${URL}`);
    const browser = await chromium.launch({
        headless: true,
        args: ['--disable-gpu-sandbox', '--no-sandbox', '--autoplay-policy=no-user-gesture-required']
    });
    const context = await browser.newContext({ viewport: VIEWPORT });
    const page = await context.newPage();

    const consoleMessages = [];
    const pageErrors = [];

    page.on('console', msg => {
        const entry = `[${msg.type()}] ${msg.text()}`;
        consoleMessages.push(entry);
        if (msg.type() === 'error') console.log('  !! CONSOLE ERROR:', msg.text());
    });
    page.on('pageerror', err => {
        pageErrors.push(err.message + '\n' + err.stack);
        console.log('  !! PAGE ERROR:', err.message);
    });
    page.on('requestfailed', req => {
        console.log(`  !! REQUEST FAILED: ${req.url()} — ${req.failure()?.errorText}`);
    });

    // ── Step 1: navigate + wait for Unity init ───────────────────────
    console.log('[LiveTest] Navigating...');
    await page.goto(URL, { waitUntil: 'networkidle', timeout: 60000 });

    // Wait for unity-canvas element to exist, then for the Unity runtime
    // (unityInstance) to be created. The Unity loader sets window.unityInstance
    // after the .wasm + .data finish loading.
    console.log('[LiveTest] Waiting for Unity canvas...');
    await page.waitForSelector('#unity-canvas', { timeout: 30000 });

    console.log('[LiveTest] Waiting for unityInstance (WebGL runtime init)...');
    try {
        await page.waitForFunction(() => !!window.unityInstance, { timeout: 120000 });
        console.log('[LiveTest] unityInstance ready ✓');
    } catch (e) {
        console.log('[LiveTest] WARNING: unityInstance not detected within 120s — continuing anyway');
    }

    // Give the game extra time to finish Bootstrap → Game scene transition
    await page.waitForTimeout(6000);

    // Screenshot 1: MainMenu (hopefully)
    await page.screenshot({ path: path.join(OUT_DIR, '01_mainmenu.png'), fullPage: false });
    console.log('[LiveTest] Screenshot 01_mainmenu.png');

    // Check for any errors accumulated so far
    const earlyErrors = pageErrors.length + consoleMessages.filter(m => m.startsWith('[error]')).length;
    console.log(`[LiveTest] Early errors: ${earlyErrors}`);

    // ── Step 2: click New Game button ────────────────────────────────
    // The MainMenu layout puts the New Game button around the middle of the
    // canvas, slightly below center. Unity's CanvasScaler uses 1080x1920
    // reference; the actual canvas pixel size depends on the viewport.
    // We query the live canvas rect and click a position that corresponds
    // to the New Game button's center in reference space.
    console.log('[LiveTest] Clicking New Game button...');
    const canvasBox = await page.$eval('#unity-canvas', el => {
        const rect = el.getBoundingClientRect();
        return { x: rect.x, y: rect.y, w: rect.width, h: rect.height };
    });
    console.log(`[LiveTest] Canvas rect: ${JSON.stringify(canvasBox)}`);

    // New Game button is at canvas ref position ~(0, 20) with size 500x80
    // in the reference 1080x1920 space. Center X = 540 (half of 1080),
    // Y = ref(1920/2 - 20) = 940 from the top in ref space.
    // Map to canvas CSS pixels:
    const refW = 1080, refH = 1920;
    const mapX = (refX) => canvasBox.x + (refX / refW) * canvasBox.w;
    const mapY = (refY) => canvasBox.y + (refY / refH) * canvasBox.h;

    const newGameX = mapX(540);  // center horizontally
    const newGameY = mapY(940);  // 940 from top (near vertical center, slightly below)

    await page.mouse.move(newGameX, newGameY);
    await page.waitForTimeout(500);
    await page.mouse.click(newGameX, newGameY);
    console.log(`[LiveTest] Clicked at (${newGameX.toFixed(0)}, ${newGameY.toFixed(0)})`);

    // Wait for Castle scene transition
    await page.waitForTimeout(5000);
    await page.screenshot({ path: path.join(OUT_DIR, '02_after_newgame.png') });
    console.log('[LiveTest] Screenshot 02_after_newgame.png');

    // ── Step 3: give the game more time (Gemini art + NPC cards) ──────
    await page.waitForTimeout(10000);
    await page.screenshot({ path: path.join(OUT_DIR, '03_castle_loaded.png') });
    console.log('[LiveTest] Screenshot 03_castle_loaded.png');

    // ── Write logs ───────────────────────────────────────────────────
    const logPath = path.join(OUT_DIR, 'console.log');
    fs.writeFileSync(logPath, [
        `# URL: ${URL}`,
        `# Timestamp: ${new Date().toISOString()}`,
        `# Console messages: ${consoleMessages.length}`,
        `# Page errors: ${pageErrors.length}`,
        '',
        '### CONSOLE MESSAGES ###',
        ...consoleMessages,
        '',
        '### PAGE ERRORS ###',
        ...pageErrors,
    ].join('\n'));
    console.log(`[LiveTest] Log written to ${logPath}`);

    // Summary
    console.log('');
    console.log('═══ SUMMARY ═══');
    console.log(`Total console messages: ${consoleMessages.length}`);
    console.log(`Page errors: ${pageErrors.length}`);
    const errorMessages = consoleMessages.filter(m => m.startsWith('[error]'));
    console.log(`Console errors: ${errorMessages.length}`);
    if (errorMessages.length > 0) {
        console.log('--- Errors ---');
        errorMessages.slice(0, 10).forEach(m => console.log('  ' + m));
    }
    if (pageErrors.length > 0) {
        console.log('--- Page exceptions ---');
        pageErrors.slice(0, 5).forEach(e => console.log('  ' + e.split('\n')[0]));
    }

    await browser.close();
    console.log('[LiveTest] Done.');
})().catch(err => {
    console.error('[LiveTest] FATAL:', err);
    process.exit(1);
});
