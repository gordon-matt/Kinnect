class ViewProfileViewModel {
    constructor(personId, isAdminUser) {
        this.loading = ko.observable(true);
        this.personId = personId;
        this.isAdminUser = isAdminUser;
        this.fullName = ko.observable('');
        this.profileImagePath = ko.observable(null);
        this.bio = ko.observable(null);
        this.birthInfo = ko.observable('');
        this.occupation = ko.observable(null);
        this.education = ko.observable(null);
        this.religion = ko.observable(null);
        this.hasAccount = ko.observable(false);
        this.canEdit = ko.observable(false);
        this.canEditOwn = ko.observable(false);

        this.posts = ko.observableArray([]);
        this.photos = ko.observableArray([]);
        this.videos = ko.observableArray([]);
        this.documents = ko.observableArray([]);
        this.events = ko.observableArray([]);
    }

    loadProfile = async () => {
        try {
            const personRes = await fetch(`/api/people/${this.personId}`);
            const personResult = await personRes.json();
            const person = personResult.value || personResult;

            this.fullName(`${person.givenNames} ${person.familyName}`);
            this.profileImagePath(person.profileImagePath);
            this.bio(person.bio);
            this.occupation(person.occupation);
            this.education(person.education);
            this.religion(person.religion);
            this.hasAccount(!!person.userId);

            let birth = '';
            if (person.yearOfBirth) {
                birth = person.yearOfBirth.toString();
                if (person.monthOfBirth && person.dayOfBirth) {
                    birth = `${person.yearOfBirth}-${String(person.monthOfBirth).padStart(2, '0')}-${String(person.dayOfBirth).padStart(2, '0')}`;
                }
            }
            if (person.placeOfBirth) {
                birth += (birth ? ', ' : '') + person.placeOfBirth;
            }
            this.birthInfo(birth);

            // Determine edit permission
            const meRes = await fetch('/api/people/me');
            if (meRes.ok) {
                const meResult = await meRes.json();
                const me = meResult.value || meResult;
                const isOwnProfile = me.id === this.personId;
                const isUnlinked = !person.userId;
                this.canEditOwn(isOwnProfile);
                this.canEdit(isOwnProfile || isUnlinked || this.isAdminUser);
            } else if (this.isAdminUser) {
                this.canEdit(true);
            }

            const [postsRes, photosRes, videosRes, docsRes, eventsRes] = await Promise.all([
                fetch(`/api/posts/person/${this.personId}`),
                fetch(`/api/photos/person/${this.personId}`),
                fetch(`/api/videos/person/${this.personId}`),
                fetch(`/api/documents/person/${this.personId}`),
                fetch(`/api/people/${this.personId}/events`)
            ]);

            const postsData = await postsRes.json();
            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();
            const eventsData = await eventsRes.json();

            this.posts(postsData.value || postsData || []);
            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
            this.events(eventsData.value || eventsData || []);
        } catch (err) {
            console.error('Error loading profile:', err);
        } finally {
            this.loading(false);
        }
    };

    formatDateTime(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const timeStr = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        return `${date.toLocaleDateString()} at ${timeStr}`;
    }

    eventIcon(eventType) {
        const icons = {
            'BIRT': 'bi-star',
            'DEAT': 'bi-flower1',
            'BURI': 'bi-tree',
            'CREM': 'bi-fire',
            'MARR': 'bi-heart',
            'DIV': 'bi-x-circle',
            'ENGA': 'bi-heart-half',
            'BAPM': 'bi-droplet',
            'CHR': 'bi-droplet-half',
            'CONF': 'bi-shield-check',
            'ADOP': 'bi-house-heart',
            'EMIG': 'bi-airplane',
            'IMMI': 'bi-airplane-fill',
            'NATU': 'bi-flag',
            'OCCU': 'bi-briefcase',
            'EDUC': 'bi-mortarboard',
            'RELI': 'bi-book',
            'RESI': 'bi-house',
            'EVEN': 'bi-calendar-event',
        };
        return icons[eventType] || 'bi-calendar-event';
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new ViewProfileViewModel(viewPersonId, isAdmin);
    ko.applyBindings(vm);
    await vm.loadProfile();
});
