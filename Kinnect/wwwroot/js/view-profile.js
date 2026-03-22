const MONTHS = [
    { v: null, l: '— Month —' },
    { v: 1,  l: 'January' },  { v: 2,  l: 'February' }, { v: 3,  l: 'March' },
    { v: 4,  l: 'April' },    { v: 5,  l: 'May' },       { v: 6,  l: 'June' },
    { v: 7,  l: 'July' },     { v: 8,  l: 'August' },    { v: 9,  l: 'September' },
    { v: 10, l: 'October' },  { v: 11, l: 'November' },  { v: 12, l: 'December' }
];

function daysInMonth(monthObs, yearObs) {
    return ko.computed(() => {
        const m = parseInt(ko.unwrap(monthObs)) || 0;
        const y = parseInt(ko.unwrap(yearObs)) || 2000;
        const count = m ? new Date(y, m, 0).getDate() : 31;
        return [null, ...Array.from({ length: count }, (_, i) => i + 1)];
    });
}

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
        this.spouses = ko.observableArray([]);

        // Merged timeline with spouse events (item 1)
        this.timelineItems = ko.computed(() => {
            const items = [];

            for (const ev of this.events()) {
                items.push({ ...ev, _source: 'event' });
            }

            for (const sp of this.spouses()) {
                const name = sp.displayName;
                if (sp.engagementYear) {
                    items.push({
                        _source: 'spouse', id: null, eventType: 'ENGA', eventTypeLabel: 'Engagement',
                        year: sp.engagementYear, month: sp.engagementMonth, day: sp.engagementDay,
                        place: null, description: `To ${name}`
                    });
                }
                if (sp.marriageYear) {
                    items.push({
                        _source: 'spouse', id: null, eventType: 'MARR', eventTypeLabel: 'Marriage',
                        year: sp.marriageYear, month: sp.marriageMonth, day: sp.marriageDay,
                        place: null, description: `To ${name}`
                    });
                }
                if (sp.divorceYear) {
                    items.push({
                        _source: 'spouse', id: null, eventType: 'DIV', eventTypeLabel: 'Divorce',
                        year: sp.divorceYear, month: sp.divorceMonth, day: sp.divorceDay,
                        place: null, description: `From ${name}`
                    });
                }
            }

            return items.sort((a, b) => {
                const ay = a.year ?? 9999, by = b.year ?? 9999;
                if (ay !== by) return ay - by;
                const am = a.month ?? 0, bm = b.month ?? 0;
                if (am !== bm) return am - bm;
                return (a.day ?? 0) - (b.day ?? 0);
            });
        });

        this.photoLightboxUrl = ko.observable(null);

        this.editingEventId = ko.observable(null);
        this.editEventType = ko.observable('BIRT');
        this.editEventYear = ko.observable(null);
        this.editEventMonth = ko.observable(null);
        this.editEventDay = ko.observable(null);
        this.editEventDayOptions = daysInMonth(this.editEventMonth, this.editEventYear);
        this.editEventPlace = ko.observable('');
        this.editEventDescription = ko.observable('');

        this.editingPhotoId = ko.observable(null);
        this.editPhotoTitle = ko.observable('');
        this.editPhotoDescription = ko.observable('');
        this.editPhotoYear = ko.observable(null);
        this.editPhotoMonth = ko.observable(null);
        this.editPhotoDay = ko.observable(null);
        this.editPhotoDayOptions = daysInMonth(this.editPhotoMonth, this.editPhotoYear);
        this.editPhotoTagsText = ko.observable('');

        this.MONTHS = MONTHS;
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

            const [postsRes, photosRes, videosRes, docsRes, eventsRes, spousesRes] = await Promise.all([
                fetch(`/api/posts/person/${this.personId}`),
                fetch(`/api/photos/person/${this.personId}`),
                fetch(`/api/videos/person/${this.personId}`),
                fetch(`/api/documents/person/${this.personId}`),
                fetch(`/api/people/${this.personId}/events`),
                fetch(`/api/people/${this.personId}/spouses`)
            ]);

            const postsData = await postsRes.json();
            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();
            const eventsData = await eventsRes.json();
            const spousesData = await spousesRes.json();

            this.posts(postsData.value || postsData || []);
            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
            this.events(eventsData.value || eventsData || []);

            const spouseList = spousesData.value || spousesData || [];
            this.spouses(spouseList.map(s => ({
                spousePersonId: s.spousePersonId,
                displayName: `${s.givenNames} ${s.familyName}`.trim(),
                marriageYear: s.marriageYear ?? null,
                marriageMonth: s.marriageMonth ?? null,
                marriageDay: s.marriageDay ?? null,
                divorceYear: s.divorceYear ?? null,
                divorceMonth: s.divorceMonth ?? null,
                divorceDay: s.divorceDay ?? null,
                engagementYear: s.engagementYear ?? null,
                engagementMonth: s.engagementMonth ?? null,
                engagementDay: s.engagementDay ?? null
            })));

            this.birthInfo(this.formatBirthHeader(this.events()));
        } catch (err) {
            console.error('Error loading profile:', err);
        } finally {
            this.loading(false);
        }
    };

    // Item 6: derive birth info from BIRT event's place (not person.placeOfBirth)
    formatBirthHeader(events) {
        const birt = (events || []).find(e => e.eventType === 'BIRT');
        if (!birt) return '';
        const parts = [];
        if (birt.year) {
            parts.push(birt.month && birt.day
                ? `${birt.year}-${String(birt.month).padStart(2,'0')}-${String(birt.day).padStart(2,'0')}`
                : String(birt.year));
        }
        if (birt.place) parts.push(birt.place);
        return parts.join(' · ');
    }

    formatDateTime(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        return `${date.toLocaleDateString()} at ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    }

    formatPhotoDate(photo) {
        if (photo == null || photo.yearTaken == null) return '';
        const y = photo.yearTaken;
        if (photo.monthTaken != null && photo.dayTaken != null)
            return `${y}-${String(photo.monthTaken).padStart(2,'0')}-${String(photo.dayTaken).padStart(2,'0')}`;
        if (photo.monthTaken != null) return `${y}-${String(photo.monthTaken).padStart(2,'0')}`;
        return String(y);
    }

    eventIcon(eventType) {
        const icons = {
            BIRT: 'bi-star', DEAT: 'bi-flower1', BURI: 'bi-tree', CREM: 'bi-fire',
            MARR: 'bi-heart', DIV: 'bi-x-circle', ENGA: 'bi-heart-half',
            BAPM: 'bi-droplet', CHR: 'bi-droplet-half', CONF: 'bi-shield-check',
            ADOP: 'bi-house-heart', EMIG: 'bi-airplane', IMMI: 'bi-airplane-fill',
            NATU: 'bi-flag', MARB: 'bi-megaphone', MARL: 'bi-file-earmark-text',
            OCCU: 'bi-briefcase', EDUC: 'bi-mortarboard', RELI: 'bi-book',
            RESI: 'bi-house', EVEN: 'bi-calendar-event'
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

        const tags = this.editPhotoTagsText().split(',').map(t => t.trim()).filter(Boolean);
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
            } else toast.error('Failed to update photo.');
        } catch { toast.error('Error updating photo.'); }
    };

    startEditEvent = (ev) => {
        if (ev._source === 'spouse') return; // spouse events not editable from view profile
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
            } else toast.error('Failed to update event.');
        } catch { toast.error('Error updating event.'); }
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new ViewProfileViewModel(viewPersonId, isAdmin);
    ko.applyBindings(vm);
    await vm.loadProfile();
});
