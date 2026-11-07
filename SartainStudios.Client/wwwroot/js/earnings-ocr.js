let library;

function loadLibrary() {
    if (!library) {
        library = new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = new URL("ocr/tesseract.min.js", document.baseURI).href;
            script.onload = () => resolve(window.Tesseract);
            script.onerror = () => reject(new Error("Could not load the OCR engine. Check your connection and try again."));
            document.head.appendChild(script);
        }).catch(error => {
            library = null;
            throw error;
        });
    }
    return library;
}

function toBlob(canvas) {
    return new Promise((resolve, reject) => canvas.toBlob(
        blob => blob ? resolve(blob) : reject(new Error("Could not prepare the screenshot.")),
        "image/png"));
}

export async function readScreenshots(input) {
    const files = Array.from(input.files || []);
    if (files.length === 0 || files.length > 10)
        throw new Error("Choose between 1 and 10 screenshots.");
    if (files.some(file => !file.type.startsWith("image/") || file.size > 25 * 1024 * 1024))
        throw new Error("Each file must be an image no larger than 25 MB.");

    const Tesseract = await loadLibrary();
    const assets = new URL("ocr/", document.baseURI).href.replace(/\/$/, "");
    const worker = await Tesseract.createWorker("eng", 1, {
        workerPath: `${assets}/worker.min.js`,
        corePath: assets,
        langPath: assets
    });
    try {
        const results = [];
        for (const file of files) {
            const image = await createImageBitmap(file);
            try {
                if (image.width * image.height > 80000000 || image.width > 8192)
                    throw new Error(`${file.name}: image is too large; use smaller screenshots.`);
                const lines = [];
                const height = Math.min(10000, Math.floor(16000000 / image.width));
                const overlap = Math.min(600, Math.floor(height / 4));
                for (let top = 0; top < image.height; top += height - overlap) {
                    const sliceHeight = Math.min(height, image.height - top);
                    const canvas = document.createElement("canvas");
                    canvas.width = image.width;
                    canvas.height = sliceHeight;
                    canvas.getContext("2d").drawImage(image, 0, top, image.width, sliceHeight, 0, 0, image.width, sliceHeight);
                    const result = await worker.recognize(await toBlob(canvas), {}, { blocks: true });
                    for (const block of result.data.blocks || []) {
                        for (const paragraph of block.paragraphs || []) {
                            for (const line of paragraph.lines || []) {
                                const middle = (line.bbox.y0 + line.bbox.y1) / 2;
                                if ((top === 0 || middle >= overlap / 2) &&
                                    (top + sliceHeight === image.height || middle < sliceHeight - overlap / 2))
                                    lines.push({ y: top + middle, text: line.text.trim() });
                            }
                        }
                    }
                    if (top + sliceHeight === image.height) break;
                }
                lines.sort((a, b) => a.y - b.y);
                results.push({ name: file.name, text: lines.map(line => line.text).join("\n") });
            } finally {
                image.close();
            }
        }
        return results.map((result, index) => ({ ...result, url: URL.createObjectURL(files[index]) }));
    } finally {
        await worker.terminate();
    }
}

export function releaseScreenshots(urls) {
    for (const url of urls) URL.revokeObjectURL(url);
}