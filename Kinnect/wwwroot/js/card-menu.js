let stop = null;

export function place() {
    release();
    const root = document.querySelector(".kinnect-card-menu.is-open");
    const menu = root?.querySelector(".nf-menu-list");
    const toggle = root?.querySelector(".kinnect-card-menu-toggle");
    if (!menu || !toggle) {
        return;
    }

    const update = () => {
        const toggleBox = toggle.getBoundingClientRect();
        menu.style.top = "0px";
        menu.style.left = "0px";
        const menuBox = menu.getBoundingClientRect();
        const gap = 4;
        let left = toggleBox.right + gap;
        if (left + menuBox.width > window.innerWidth - 8) {
            left = toggleBox.left - menuBox.width - gap;
        }

        left = Math.max(8, Math.min(left, window.innerWidth - menuBox.width - 8));
        let top = toggleBox.top;
        if (top + menuBox.height > window.innerHeight - 8) {
            top = Math.max(8, toggleBox.bottom - menuBox.height);
        }

        menu.style.top = `${top}px`;
        menu.style.left = `${left}px`;
        menu.classList.add("is-placed");
    };

    update();
    const scrollers = [window];
    const content = document.querySelector(".nf-content");
    if (content) {
        scrollers.push(content);
    }

    for (const scroller of scrollers) {
        scroller.addEventListener("scroll", update, { passive: true });
    }

    window.addEventListener("resize", update);
    stop = () => {
        for (const scroller of scrollers) {
            scroller.removeEventListener("scroll", update);
        }

        window.removeEventListener("resize", update);
        stop = null;
    };
}

export function release() {
    stop?.();
}
