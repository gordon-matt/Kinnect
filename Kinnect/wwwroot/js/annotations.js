let annotator = null;
let label = null;
let dotnet = null;

function commentOf(annotation) {
    const raw = annotation?.bodies ?? annotation?.body;
    const bodies = Array.isArray(raw) ? raw : raw ? [raw] : [];
    const body = bodies.find(item =>
        (item.purpose === "commenting" || item.purpose === "describing") && typeof item.value === "string");
    return body?.value || "";
}

function withComment(annotation, text) {
    return {
        ...annotation,
        bodies: [{ type: "TextualBody", purpose: "commenting", value: text }],
    };
}

function hover() {
    if (label) {
        return label;
    }

    label = document.createElement("div");
    label.className = "kinnect-annotation-label";
    label.style.display = "none";
    document.body.appendChild(label);
    document.addEventListener("mousemove", event => {
        if (!label || label.style.display === "none") {
            return;
        }

        label.style.left = `${event.clientX + 14}px`;
        label.style.top = `${event.clientY + 14}px`;
    });
    return label;
}

function showLabel(text) {
    const el = hover();
    el.textContent = text;
    el.style.display = "block";
}

function hideLabel() {
    if (label) {
        label.style.display = "none";
    }
}

async function persist() {
    if (!dotnet || !annotator?.getAnnotations) {
        return;
    }

    const json = JSON.stringify(annotator.getAnnotations());
    await dotnet.invokeMethodAsync("SaveAnnotations", json);
}

export async function attach(image, annotationsJson, editable, callback) {
    clear();
    if (typeof Annotorious === "undefined" || !image) {
        return;
    }

    if (!image.complete) {
        await new Promise(resolve => image.addEventListener("load", resolve, { once: true }));
    }

    dotnet = callback;
    const anno = Annotorious.createImageAnnotator(image);
    annotator = anno;
    anno.setDrawingEnabled?.(!!editable);
    anno.on?.("mouseEnterAnnotation", annotation => {
        const text = commentOf(annotation);
        if (text) {
            showLabel(text);
        }
    });
    anno.on?.("mouseLeaveAnnotation", hideLabel);

    if (editable) {
        anno.on?.("createAnnotation", async annotation => {
            const text = window.prompt("Annotation text:", "");
            if (text && text.trim()) {
                await anno.updateAnnotation?.(withComment(annotation, text.trim()));
            }

            await persist();
        });
        anno.on?.("updateAnnotation", persist);
        anno.on?.("deleteAnnotation", persist);
    }

    if (annotationsJson) {
        try {
            await anno.setAnnotations(JSON.parse(annotationsJson));
        } catch {
            // Stored notes that are not Annotorious JSON stay hidden.
        }
    }
}

export function clear() {
    hideLabel();
    if (annotator) {
        try {
            annotator.destroy();
        } catch {
            // The image may already be gone.
        }

        annotator = null;
    }

    dotnet = null;
}
