(function () {
    const explicitPersonId =
        typeof window.profilePersonId !== 'undefined' && window.profilePersonId !== null
            ? Number(window.profilePersonId)
            : null;

    class ProfileViewModel {
        constructor() {
            this.loading = ko.observable(true);
            this.personId = ko.observable(null);
            this.givenNames = ko.observable('');
            this.familyName = ko.observable('');
            this.isMaleStr = ko.observable('true');
            this.yearOfBirth = ko.observable(null);
            this.monthOfBirth = ko.observable(null);
            this.dayOfBirth = ko.observable(null);
            this.placeOfBirth = ko.observable('');
            this.bio = ko.observable('');
            this.profileImagePath = ko.observable(null);
            this.latitude = ko.observable(null);
            this.longitude = ko.observable(null);
            this.occupation = ko.observable('');
            this.education = ko.observable('');
            this.religion = ko.observable('');
            this.note = ko.observable('');

            this.fullName = ko.computed(() => `${this.givenNames()} ${this.familyName()}`);
            this.locationText = ko.computed(() => {
                const lat = this.latitude();
                const lng = this.longitude();
                return lat && lng ? `${lat}, ${lng}` : '';
            });

            this.posts = ko.observableArray([]);
            this.photos = ko.observableArray([]);
            this.videos = ko.observableArray([]);
            this.documents = ko.observableArray([]);
            this.events = ko.observableArray([]);

            this.isUploadingPhoto = ko.observable(false);
            this.photoTitle = ko.observable('');
            this.photoDescription = ko.observable('');
            this.photoYearTaken = ko.observable(null);
            this.photoMonthTaken = ko.observable(null);
            this.photoDayTaken = ko.observable(null);

            this.isUploadingVideo = ko.observable(false);
            this.videoTitle = ko.observable('');
            this.videoDescription = ko.observable('');

            this.isUploadingDocument = ko.observable(false);
            this.documentTitle = ko.observable('');
            this.documentDescription = ko.observable('');

            this.isAddingEvent = ko.observable(false);
            this.newEventType = ko.observable('BIRT');
            this.newEventYear = ko.observable(null);
            this.newEventMonth = ko.observable(null);
            this.newEventDay = ko.observable(null);
            this.newEventPlace = ko.observable('');
            this.newEventDescription = ko.observable('');

            this.editingEventId = ko.observable(null);
            this.editEventType = ko.observable('BIRT');
            this.editEventYear = ko.observable(null);
            this.editEventMonth = ko.observable(null);
            this.editEventDay = ko.observable(null);
            this.editEventPlace = ko.observable('');
            this.editEventDescription = ko.observable('');

            this.photoLightboxUrl = ko.observable(null);

            this.editingPhotoId = ko.observable(null);
            this.editPhotoTitle = ko.observable('');
            this.editPhotoDescription = ko.observable('');
            this.editPhotoYear = ko.observable(null);
            this.editPhotoMonth = ko.observable(null);
            this.editPhotoDay = ko.observable(null);
            this.editPhotoTagsText = ko.observable('');

            this._explicitPersonId = explicitPersonId;
            this._tagifyInstances = {};
            this._personSnapshot = null;
        }

        loadProfile = async () => {
            try {
                const url = this._explicitPersonId != null ? `/api/people/${this._explicitPersonId}` : '/api/people/me';
                const response = await fetch(url);
                const result = await response.json();
                const person = result.value || result;
                this._personSnapshot = person;

                this.personId(person.id);
                this.givenNames(person.givenNames);
                this.familyName(person.familyName);
                this.isMaleStr(person.isMale ? 'true' : 'false');
                this.yearOfBirth(person.yearOfBirth);
                this.monthOfBirth(person.monthOfBirth);
                this.dayOfBirth(person.dayOfBirth);
                this.placeOfBirth(person.placeOfBirth || '');
                this.bio(person.bio || '');
                this.profileImagePath(person.profileImagePath);
                this.latitude(person.latitude);
                this.longitude(person.longitude);
                this.occupation(person.occupation || '');
                this.education(person.education || '');
                this.religion(person.religion || '');
                this.note(person.note || '');

                await this.loadContent();
            } catch (err) {
                console.error('Error loading profile:', err);
            } finally {
                this.loading(false);
            }
        };

        loadContent = async () => {
            const pid = this.personId();
            if (!pid) return;

            const [postsRes, photosRes, videosRes, docsRes, eventsRes] = await Promise.all([
                fetch(`/api/posts/person/${pid}`),
                fetch(`/api/photos/person/${pid}`),
                fetch(`/api/videos/person/${pid}`),
                fetch(`/api/documents/person/${pid}`),
                fetch(`/api/people/${pid}/events`)
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
        };

        saveProfile = async () => {
            const snapshot = this._personSnapshot || {};
            const body = {
                givenNames: this.givenNames(),
                familyName: this.familyName(),
                isMale: this.isMaleStr() === 'true',
                yearOfBirth: this.yearOfBirth() || null,
                monthOfBirth: this.monthOfBirth() || null,
                dayOfBirth: this.dayOfBirth() || null,
                yearOfDeath: snapshot.yearOfDeath ?? null,
                monthOfDeath: snapshot.monthOfDeath ?? null,
                dayOfDeath: snapshot.dayOfDeath ?? null,
                placeOfBirth: this.placeOfBirth() || null,
                placeOfDeath: snapshot.placeOfDeath ?? null,
                bio: this.bio() || null,
                latitude: this.latitude() || null,
                longitude: this.longitude() || null,
                fatherId: snapshot.fatherId ?? null,
                motherId: snapshot.motherId ?? null,
                occupation: this.occupation() || null,
                education: this.education() || null,
                religion: this.religion() || null,
                note: this.note() || null
            };

            try {
                const response = await fetch(`/api/people/${this.personId()}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });

                if (response.ok) {
                    this._personSnapshot = { ...snapshot, ...body };
                    toast.success('Profile saved!');
                } else {
                    toast.error('Failed to save profile.');
                }
            } catch {
                toast.error('Error saving profile.');
            }
        };

        uploadProfileImage = async (vm, event) => {
            const file = event.target.files[0];
            if (!file) return;

            const formData = new FormData();
            formData.append('file', file);

            try {
                const response = await fetch(`/api/people/${this.personId()}/profile-image`, {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    await this.loadProfile();
                    toast.success('Profile image updated!');
                } else {
                    toast.error('Failed to upload image.');
                }
            } catch {
                toast.error('Error uploading image.');
            }
        };

        // ── Timeline ───────────────────────────────────────────────────────────
        showAddEvent = () => {
            this.newEventType('BIRT');
            this.newEventYear(null);
            this.newEventMonth(null);
            this.newEventDay(null);
            this.newEventPlace('');
            this.newEventDescription('');
            this.isAddingEvent(true);
        };

        cancelAddEvent = () => this.isAddingEvent(false);

        addEvent = async () => {
            const pid = this.personId();
            if (!pid) return;

            const body = {
                eventType: this.newEventType(),
                year: this.newEventYear() || null,
                month: this.newEventMonth() || null,
                day: this.newEventDay() || null,
                place: this.newEventPlace() || null,
                description: this.newEventDescription() || null
            };

            try {
                const res = await fetch(`/api/people/${pid}/events`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });

                if (res.ok) {
                    this.isAddingEvent(false);
                    await this.loadContent();
                    toast.success('Event added!');
                } else {
                    toast.error('Failed to add event.');
                }
            } catch {
                toast.error('Error adding event.');
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
            const pid = this.personId();
            const eid = this.editingEventId();
            if (!pid || !eid) return;

            const body = {
                eventType: this.editEventType(),
                year: this.editEventYear() || null,
                month: this.editEventMonth() || null,
                day: this.editEventDay() || null,
                place: this.editEventPlace() || null,
                description: this.editEventDescription() || null
            };

            try {
                const res = await fetch(`/api/people/${pid}/events/${eid}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });

                if (res.ok) {
                    bootstrap.Modal.getInstance(document.getElementById('editEventModal'))?.hide();
                    this.editingEventId(null);
                    await this.loadContent();
                    toast.success('Event updated!');
                } else {
                    toast.error('Failed to update event.');
                }
            } catch {
                toast.error('Error updating event.');
            }
        };

        deleteEvent = async (eventId) => {
            const pid = this.personId();
            if (!confirm('Delete this event?')) return;
            try {
                const res = await fetch(`/api/people/${pid}/events/${eventId}`, { method: 'DELETE' });
                if (res.ok) {
                    await this.loadContent();
                    toast.success('Event deleted.');
                } else {
                    toast.error('Failed to delete event.');
                }
            } catch {
                toast.error('Error deleting event.');
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
                OCCU: 'bi-briefcase',
                EDUC: 'bi-mortarboard',
                RELI: 'bi-book',
                RESI: 'bi-house',
                EVEN: 'bi-calendar-event'
            };
            return icons[eventType] || 'bi-calendar-event';
        }

        // ── Photos ─────────────────────────────────────────────────────────────
        formatPhotoDate = (photo) => {
            if (photo == null || photo.yearTaken == null) return '';
            const y = photo.yearTaken;
            if (photo.monthTaken != null && photo.dayTaken != null) {
                return `${y}-${String(photo.monthTaken).padStart(2, '0')}-${String(photo.dayTaken).padStart(2, '0')}`;
            }
            if (photo.monthTaken != null) return `${y}-${String(photo.monthTaken).padStart(2, '0')}`;
            return String(y);
        };

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
                    await this.loadContent();
                    toast.success('Photo updated!');
                } else {
                    toast.error('Failed to update photo.');
                }
            } catch {
                toast.error('Error updating photo.');
            }
        };

        // ── Media uploads ──────────────────────────────────────────────────────
        showPhotoUpload = () => {
            this.isUploadingPhoto(true);
            this.photoYearTaken(null);
            this.photoMonthTaken(null);
            this.photoDayTaken(null);
            this.initTagify('photoTags');
        };
        cancelPhotoUpload = () => this.isUploadingPhoto(false);

        showVideoUpload = () => {
            this.isUploadingVideo(true);
            this.initTagify('videoTags');
        };
        cancelVideoUpload = () => this.isUploadingVideo(false);

        showDocumentUpload = () => {
            this.isUploadingDocument(true);
            this.initTagify('documentTags');
        };
        cancelDocumentUpload = () => this.isUploadingDocument(false);

        initTagify = (inputId) => {
            setTimeout(async () => {
                const input = document.getElementById(inputId);
                if (!input || this._tagifyInstances[inputId]) return;

                const tagsResponse = await fetch('/api/tags');
                const tagsResult = await tagsResponse.json();
                const whitelist = (tagsResult.value || tagsResult || []).map((t) => t.name);

                this._tagifyInstances[inputId] = new Tagify(input, {
                    whitelist,
                    enforceWhitelist: false,
                    maxTags: 20,
                    dropdown: { maxItems: 20, enabled: 1, closeOnSelect: false }
                });
            }, 100);
        };

        getTagValues = (inputId) => {
            const tagify = this._tagifyInstances[inputId];
            return tagify ? tagify.value.map((t) => t.value) : [];
        };

        uploadPhoto = async () => {
            const fileInput = document.getElementById('photoFile');
            if (!fileInput.files[0]) return;

            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            formData.append('title', this.photoTitle());
            formData.append('description', this.photoDescription());
            formData.append('tags', this.getTagValues('photoTags').join(','));
            const y = this.photoYearTaken();
            const mo = this.photoMonthTaken();
            const d = this.photoDayTaken();
            if (y != null && y !== '') formData.append('yearTaken', String(y));
            if (mo != null && mo !== '') formData.append('monthTaken', String(mo));
            if (d != null && d !== '') formData.append('dayTaken', String(d));

            const response = await fetch('/api/photos', { method: 'POST', body: formData });
            if (response.ok) {
                this.isUploadingPhoto(false);
                this.photoTitle('');
                this.photoDescription('');
                this.photoYearTaken(null);
                this.photoMonthTaken(null);
                this.photoDayTaken(null);
                await this.loadContent();
                toast.success('Photo uploaded!');
            } else {
                toast.error('Failed to upload photo.');
            }
        };

        uploadVideo = async () => {
            const fileInput = document.getElementById('videoFile');
            if (!fileInput.files[0]) return;

            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            formData.append('title', this.videoTitle());
            formData.append('description', this.videoDescription());
            formData.append('tags', this.getTagValues('videoTags').join(','));

            const response = await fetch('/api/videos', { method: 'POST', body: formData });
            if (response.ok) {
                this.isUploadingVideo(false);
                this.videoTitle('');
                this.videoDescription('');
                await this.loadContent();
                toast.success('Video uploaded!');
            } else {
                toast.error('Failed to upload video.');
            }
        };

        uploadDocument = async () => {
            const fileInput = document.getElementById('documentFile');
            if (!fileInput.files[0]) return;

            const formData = new FormData();
            formData.append('file', fileInput.files[0]);
            formData.append('title', this.documentTitle());
            formData.append('description', this.documentDescription());
            formData.append('tags', this.getTagValues('documentTags').join(','));

            const response = await fetch('/api/documents', { method: 'POST', body: formData });
            if (response.ok) {
                this.isUploadingDocument(false);
                this.documentTitle('');
                this.documentDescription('');
                await this.loadContent();
                toast.success('Document uploaded!');
            } else {
                toast.error('Failed to upload document.');
            }
        };
    }

    document.addEventListener('DOMContentLoaded', async () => {
        const vm = new ProfileViewModel();
        ko.applyBindings(vm);
        await vm.loadProfile();
    });
})();
