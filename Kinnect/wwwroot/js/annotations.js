let annotator = null;
let label = null;
let selectedId = null;

function commentOf(annotation) {
    const raw = annotation?.bodies ?? annotation?.body;
    const bodies = Array.isArray(raw) ? raw : raw ? [raw] : [];
    const body = bodies.find(item =>
        (item.purpose === "commenting" || item.purpose === "describing") && typeof item.value === "string");
    return body?.value || "";
}

function withComment(annotation, text) {
    const bodies = [{ type: "TextualBody", purpose: "commenting", value: text }];
    return { ...annotation, bodies };
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

export async function attach(image, annotationsJson, editable) {
    clear();
    if (typeof Annotorious === "undefined" || !image) {
        return;
    }

    if (!image.complete) {
        await new Promise(resolve => image.addEventListener("load", resolve, { once: true }));
    }

    const anno = Annotorious.createImageAnnotator(image);
    annotator = anno;
    selectedId = null;
    anno.setDrawingEnabled?.(false);
    anno.on?.("selectionChanged", selection => {
        const selected = Array.isArray(selection) ? selection[0] : selection;
        selectedId = selected?.id || null;
    });
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
        });
    }

    if (annotationsJson) {
        try {
            await anno.setAnnotations(JSON.parse(annotationsJson));
        } catch {
            // Stored notes that are not Annotorious JSON stay hidden.
        }
    }
}

export function setTool(tool) {
    if (!annotator) {
        return;
    }

    annotator.setDrawingTool?.(tool);
    annotator.setDrawingEnabled?.(true);
}

export async function editSelected() {
    if (!annotator || !selectedId) {
        return "none";
    }

    const all = await annotator.getAnnotations();
    const selected = all.find(item => item.id === selectedId);
    if (!selected) {
        return "none";
    }

    const text = window.prompt("Annotation text:", commentOf(selected));
    if (text == null) {
        return "cancel";
    }

    await annotator.updateAnnotation?.(withComment(selected, text.trim()));
    return "ok";
}

export async function deleteSelected() {
    if (!annotator || !selectedId) {
        return "none";
    }

    if (!window.confirm("Delete selected annotation?")) {
        return "cancel";
    }

    annotator.removeAnnotation?.(selectedId);
    selectedId = null;
    return "ok";
}

export async function read() {
    if (!annotator?.getAnnotations) {
        return "[]";
    }

    const annotations = await annotator.getAnnotations();
    return JSON.stringify(annotations);
}

export function clear() {
    hideLabel();
    selectedId = null;
    if (annotator) {
        try {
            annotator.destroy();
        } catch {
            // The image may already be gone.
        }

        annotator = null;
    }
}
