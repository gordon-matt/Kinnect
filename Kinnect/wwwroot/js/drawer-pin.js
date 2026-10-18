let stop = null;

export function pin(stage) {
    release();
    const drawer = stage?.querySelector(".kinnect-add-drawer");
    if (!drawer) {
        return;
    }

    const place = () => {
        const stageBox = stage.getBoundingClientRect();
        const top = Math.max(stageBox.top, 8);
        drawer.style.position = "fixed";
        drawer.style.top = `${top}px`;
        drawer.style.bottom = "0px";
        drawer.style.right = `${Math.max(12, window.innerWidth - stageBox.right)}px`;
        drawer.style.width = "min(360px, 92vw)";
        drawer.style.maxHeight = "none";
        drawer.style.height = "auto";
    };

    place();
    const scrollers = [window];
    const content = document.querySelector(".nf-content");
    if (content) {
        scrollers.push(content);
    }

    for (const scroller of scrollers) {
        scroller.addEventListener("scroll", place, { passive: true });
    }
    window.addEventListener("resize", place);
    stop = () => {
        for (const scroller of scrollers) {
            scroller.removeEventListener("scroll", place);
        }
        window.removeEventListener("resize", place);
        drawer.style.position = "";
        drawer.style.top = "";
        drawer.style.bottom = "";
        drawer.style.right = "";
        drawer.style.width = "";
        drawer.style.maxHeight = "";
        drawer.style.height = "";
        stop = null;
    };
}

export function release() {
    stop?.();
}
