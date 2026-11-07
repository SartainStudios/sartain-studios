const fs = require("node:fs");
const path = require("node:path");

const target = path.join(__dirname, "wwwroot", "ocr");
fs.mkdirSync(target, { recursive: true });

function copy(source, name) {
    fs.copyFileSync(path.join(__dirname, "node_modules", source), path.join(target, name));
}

copy("tesseract.js/dist/tesseract.min.js", "tesseract.min.js");
copy("tesseract.js/dist/worker.min.js", "worker.min.js");
for (const name of fs.readdirSync(path.join(__dirname, "node_modules", "tesseract.js-core"))) {
    if (name.endsWith("-lstm.wasm.js"))
        copy(`tesseract.js-core/${name}`, name);
}
copy("@tesseract.js-data/eng/4.0.0_best_int/eng.traineddata.gz", "eng.traineddata.gz");