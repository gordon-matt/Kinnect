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
        const el = document.getElementById('homeLightboxModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
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
