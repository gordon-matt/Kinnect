class HomeViewModel {
    constructor() {
        this.feedItems = ko.observableArray([]);
        this.loading = ko.observable(true);
        this.newPostContent = ko.observable('');
        this.page = ko.observable(1);
        this.hasMore = ko.observable(false);

        // Photo lightbox (item 12)
        this.lightboxUrl = ko.observable(null);
        this.lightboxTitle = ko.observable('');
        this.lightboxHasAnnotations = ko.observable(false);
        this.lightboxShowAnnotations = ko.observable(true);
        this._lightboxAnnotationsJson = null;
        this._lightboxAnno = null;
        this._annotationHoverLabelEl = null;
        this._lastMouseX = 0;
        this._lastMouseY = 0;

        this.lightboxShowAnnotations.subscribe(() => this.syncLightboxAnnotations());
    }

    loadFeed = async () => {
        try {
            const response = await fetch(`/api/feed?page=${this.page()}&pageSize=20`);
            const data = await response.json();
            const items = (data.value || data || []).map(item => ({
                type: ko.observable(item.type),
                id: ko.observable(item.id),
                authorName: ko.observable(item.authorName),
                authorProfileImage: ko.observable(item.authorProfileImage),
                authorPersonId: ko.observable(item.authorPersonId),
                content: ko.observable(item.content),
                title: ko.observable(item.title),
                thumbnailPath: ko.observable(item.thumbnailPath),
                filePath: ko.observable(item.filePath),
                annotationsJson: ko.observable(item.annotationsJson || null),
                hasAnnotations: !!(item.annotationsJson && item.annotationsJson.trim().length > 0),
                createdAtUtc: ko.observable(item.createdAtUtc),
                timeAgo: this.getTimeAgo(item.createdAtUtc)
            }));

            if (this.page() === 1) {
                this.feedItems(items);
            } else {
                this.feedItems.push(...items);
            }
            this.hasMore(items.length >= 20);
        } catch (err) {
            console.error('Error loading feed:', err);
        } finally {
            this.loading(false);
        }
    };

    createPost = async () => {
        const content = this.newPostContent();
        if (!content) return;

        try {
            const response = await fetch('/api/posts', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ content })
            });

            if (response.ok) {
                this.newPostContent('');
                this.page(1);
                await this.loadFeed();
                toast.success('Post created!');
            } else {
                toast.error('Failed to create post.');
            }
        } catch (err) {
            toast.error('Error creating post.');
        }
    };

    loadMore = () => {
        if (this.loading()) return;
        this.page(this.page() + 1);
        this.loadFeed();
    };

    openLightbox = (item) => {
        const path = item.filePath?.() || item.thumbnailPath?.();
        if (!path) return;
        this.lightboxUrl('/uploads/' + path);
        this.lightboxTitle(item.title?.() || '');
        this._lightboxAnnotationsJson = item.annotationsJson?.() || null;
        this.lightboxHasAnnotations(!!(this._lightboxAnnotationsJson && this._lightboxAnnotationsJson.trim().length > 0));
        this.lightboxShowAnnotations(true);
        const el = document.getElementById('homeLightboxModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
    };

    initLightboxAnnotorious = async () => {
        this.destroyLightboxAnnotorious();

        if (!this.lightboxHasAnnotations() || !this.lightboxShowAnnotations()) return;
        if (typeof Annotorious === 'undefined') return;

        const img = document.getElementById('homeLightboxImage');
        if (!img || !img.src) return;
        if (!img.complete) {
            await new Promise(resolve => img.addEventListener('load', resolve, { once: true }));
        }

        try {
            const anno = Annotorious.createImageAnnotator(img);
            anno.setDrawingEnabled?.(false);
            this._lightboxAnno = anno;

            this._ensureAnnotationHoverLabel();

            anno.on?.('mouseEnterAnnotation', (annotation) => {
                const text = this._getAnnotationComment(annotation);
                if (text) this._showAnnotationHoverLabel(text);
            });
            anno.on?.('mouseLeaveAnnotation', () => this._hideAnnotationHoverLabel());

            const parsed = JSON.parse(this._lightboxAnnotationsJson);
            await anno.setAnnotations(parsed);
        } catch (e) {
            console.warn('Could not initialize lightbox annotations', e);
        }
    };

    syncLightboxAnnotations = async () => {
        if (!this.lightboxHasAnnotations()) {
            this.destroyLightboxAnnotorious();
            return;
        }
        if (this.lightboxShowAnnotations()) await this.initLightboxAnnotorious();
        else this.destroyLightboxAnnotorious();
    };

    destroyLightboxAnnotorious = () => {
        if (this._lightboxAnno) {
            try { this._lightboxAnno.destroy(); } catch { /* ignore */ }
            this._lightboxAnno = null;
        }
        this._hideAnnotationHoverLabel();
    };

    _getAnnotationComment = (annotation) => {
        const rawBodies = annotation?.bodies ?? annotation?.body;
        const bodies = Array.isArray(rawBodies) ? rawBodies : (rawBodies ? [rawBodies] : []);
        const body = bodies.find(b => (b.purpose === 'commenting' || b.purpose === 'describing') && typeof b.value === 'string');
        return body?.value || '';
    };

    _ensureAnnotationHoverLabel = () => {
        if (this._annotationHoverLabelEl) return;
        const area = document.getElementById('homeLightboxImageArea');
        if (!area) return;

        const el = document.createElement('div');
        el.style.position = 'fixed';
        el.style.zIndex = '3000';
        el.style.pointerEvents = 'none';
        el.style.display = 'none';
        el.style.maxWidth = '260px';
        el.style.padding = '4px 8px';
        el.style.borderRadius = '4px';
        el.style.fontSize = '12px';
        el.style.lineHeight = '1.3';
        el.style.background = 'rgba(0,0,0,0.8)';
        el.style.color = '#fff';
        document.body.appendChild(el);
        this._annotationHoverLabelEl = el;

        area.addEventListener('mousemove', (e) => {
            this._lastMouseX = e.clientX;
            this._lastMouseY = e.clientY;
            if (this._annotationHoverLabelEl && this._annotationHoverLabelEl.style.display !== 'none') {
                this._annotationHoverLabelEl.style.left = `${this._lastMouseX + 14}px`;
                this._annotationHoverLabelEl.style.top = `${this._lastMouseY + 14}px`;
            }
        });
    };

    _showAnnotationHoverLabel = (text) => {
        if (!this._annotationHoverLabelEl) return;
        this._annotationHoverLabelEl.textContent = text;
        this._annotationHoverLabelEl.style.left = `${this._lastMouseX + 14}px`;
        this._annotationHoverLabelEl.style.top = `${this._lastMouseY + 14}px`;
        this._annotationHoverLabelEl.style.display = 'block';
    };

    _hideAnnotationHoverLabel = () => {
        if (!this._annotationHoverLabelEl) return;
        this._annotationHoverLabelEl.style.display = 'none';
    };

    getTimeAgo(dateStr) {
        const date = new Date(dateStr);
        const now = new Date();
        const diff = Math.floor((now - date) / 1000);

        if (diff < 60) return 'Just now';
        if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
        if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
        if (diff < 604800) return `${Math.floor(diff / 86400)}d ago`;

        return `${date.toLocaleDateString()} at ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new HomeViewModel();
    ko.applyBindings(vm);
    await vm.loadFeed();

    const lightboxModal = document.getElementById('homeLightboxModal');
    if (lightboxModal) {
        lightboxModal.addEventListener('shown.bs.modal', () => vm.syncLightboxAnnotations());
        lightboxModal.addEventListener('hidden.bs.modal', () => vm.destroyLightboxAnnotorious());
    }

    // Item 2: infinite scroll via IntersectionObserver
    const sentinel = document.getElementById('feedSentinel');
    if (sentinel && 'IntersectionObserver' in window) {
        const observer = new IntersectionObserver(
            (entries) => {
                if (entries[0].isIntersecting && vm.hasMore() && !vm.loading()) {
                    vm.loadMore();
                }
            },
            { rootMargin: '200px' }
        );
        observer.observe(sentinel);
    }
});
