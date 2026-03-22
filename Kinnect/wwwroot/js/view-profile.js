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

        this.photoLightboxUrl = ko.observable(null);

        this.editingEventId = ko.observable(null);
        this.editEventType = ko.observable('BIRT');
        this.editEventYear = ko.observable(null);
        this.editEventMonth = ko.observable(null);
        this.editEventDay = ko.observable(null);
        this.editEventPlace = ko.observable('');
        this.editEventDescription = ko.observable('');

        this.editingPhotoId = ko.observable(null);
        this.editPhotoTitle = ko.observable('');
        this.editPhotoDescription = ko.observable('');
        this.editPhotoYear = ko.observable(null);
        this.editPhotoMonth = ko.observable(null);
        this.editPhotoDay = ko.observable(null);
        this.editPhotoTagsText = ko.observable('');
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

            this.birthInfo('');

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
            this.birthInfo(this.formatBirthHeader(person, this.events()));
        } catch (err) {
            console.error('Error loading profile:', err);
        } finally {
            this.loading(false);
        }
    };

    formatBirthHeader(person, events) {
        const birt = (events || []).find((e) => e.eventType === 'BIRT');
        let birth = '';
        if (birt && birt.year) {
            birth =
                birt.month && birt.day
                    ? `${birt.year}-${String(birt.month).padStart(2, '0')}-${String(birt.day).padStart(2, '0')}`
                    : String(birt.year);
        }
        if (person.placeOfBirth) {
            birth += (birth ? ' · ' : '') + person.placeOfBirth;
        }
        return birth;
    }

    formatDateTime(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const timeStr = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        return `${date.toLocaleDateString()} at ${timeStr}`;
    }

    formatPhotoDate(photo) {
        if (photo == null || photo.yearTaken == null) return '';
        const y = photo.yearTaken;
        if (photo.monthTaken != null && photo.dayTaken != null) {
            return `${y}-${String(photo.monthTaken).padStart(2, '0')}-${String(photo.dayTaken).padStart(2, '0')}`;
        }
        if (photo.monthTaken != null) return `${y}-${String(photo.monthTaken).padStart(2, '0')}`;
        return String(y);
    }

    eventIcon(eventType) {
        const icons = {
            BIRT: 'bi-star',
            DEAT: 'bi-flower1',
            BURI: 'bi-tree',
            CREM: 'bi-fire',
            MARR: 'bi-heart',
            DIV: 'bi-x-circle',
            ENGA: 'bi-heart-half',
            BAPM: 'bi-droplet',
            CHR: 'bi-droplet-half',
            CONF: 'bi-shield-check',
            ADOP: 'bi-house-heart',
            EMIG: 'bi-airplane',
            IMMI: 'bi-airplane-fill',
            NATU: 'bi-flag',
            MARB: 'bi-megaphone',
            MARL: 'bi-file-earmark-text',
            OCCU: 'bi-briefcase',
            EDUC: 'bi-mortarboard',
            RELI: 'bi-book',
            RESI: 'bi-house',
            EVEN: 'bi-calendar-event'
        };
        return icons[eventType] || 'bi-calendar-event';
    }

    openPhotoLightbox = (photo) => {
        this.photoLightboxUrl('/uploads/' + photo.filePath);
        const el = document.getElementById('photoLightboxModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
    };

    startEditPhoto = (photo) => {
        this.editingPhotoId(photo.id);
        this.editPhotoTitle(photo.title);
        this.editPhotoDescription(photo.description || '');
        this.editPhotoYear(photo.yearTaken);
        this.editPhotoMonth(photo.monthTaken);
        this.editPhotoDay(photo.dayTaken);
        this.editPhotoTagsText((photo.tags || []).join(', '));
        const el = document.getElementById('editPhotoModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
    };

    saveEditPhoto = async () => {
        const id = this.editingPhotoId();
        if (!id) return;

        const tags = this.editPhotoTagsText()
            .split(',')
            .map((t) => t.trim())
            .filter(Boolean);

        const body = {
            title: this.editPhotoTitle(),
            description: this.editPhotoDescription() || null,
            yearTaken: this.editPhotoYear() || null,
            monthTaken: this.editPhotoMonth() || null,
            dayTaken: this.editPhotoDay() || null,
            tags
        };

        try {
            const res = await fetch(`/api/photos/${id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (res.ok) {
                bootstrap.Modal.getInstance(document.getElementById('editPhotoModal'))?.hide();
                this.editingPhotoId(null);
                await this.loadProfile();
                toast.success('Photo updated!');
            } else {
                toast.error('Failed to update photo.');
            }
        } catch {
            toast.error('Error updating photo.');
        }
    };

    startEditEvent = (ev) => {
        this.editingEventId(ev.id);
        this.editEventType(ev.eventType);
        this.editEventYear(ev.year);
        this.editEventMonth(ev.month);
        this.editEventDay(ev.day);
        this.editEventPlace(ev.place || '');
        this.editEventDescription(ev.description || '');
        const el = document.getElementById('editEventModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
    };

    saveEditEvent = async () => {
        const eid = this.editingEventId();
        if (!eid) return;

        const body = {
            eventType: this.editEventType(),
            year: this.editEventYear() || null,
            month: this.editEventMonth() || null,
            day: this.editEventDay() || null,
            place: this.editEventPlace() || null,
            description: this.editEventDescription() || null
        };

        try {
            const res = await fetch(`/api/people/${this.personId}/events/${eid}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (res.ok) {
                bootstrap.Modal.getInstance(document.getElementById('editEventModal'))?.hide();
                this.editingEventId(null);
                await this.loadProfile();
                toast.success('Event updated!');
            } else {
                toast.error('Failed to update event.');
            }
        } catch {
            toast.error('Error updating event.');
        }
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new ViewProfileViewModel(viewPersonId, isAdmin);
    ko.applyBindings(vm);
    await vm.loadProfile();
});
