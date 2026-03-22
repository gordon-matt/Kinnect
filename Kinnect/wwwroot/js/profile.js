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
            this.placeOfBirth = ko.observable('');
            this.bio = ko.observable('');
            this.profileImagePath = ko.observable(null);
            this.latitude = ko.observable(null);
            this.longitude = ko.observable(null);
            this.occupation = ko.observable('');
            this.education = ko.observable('');
            this.religion = ko.observable('');
            this.note = ko.observable('');

            this.locationSearchQuery = ko.observable('');
            this.locationSearchResults = ko.observableArray([]);
            this._locationSearchTimer = null;
            this._locationMap = null;
            this._locationMapMarker = null;

            this.posts = ko.observableArray([]);
            this.photos = ko.observableArray([]);
            this.videos = ko.observableArray([]);
            this.documents = ko.observableArray([]);
            this.events = ko.observableArray([]);
            this.spouses = ko.observableArray([]);

            this.fullName = ko.computed(() => `${this.givenNames()} ${this.familyName()}`);
            this.subtitleLine = ko.computed(() => {
                const evs = this.events();
                const birt = evs.find((e) => e.eventType === 'BIRT');
                const parts = [];
                if (birt && birt.year != null) {
                    parts.push(
                        birt.month && birt.day
                            ? `b. ${birt.year}-${String(birt.month).padStart(2, '0')}-${String(birt.day).padStart(2, '0')}`
                            : `b. ${birt.year}`
                    );
                }
                const pob = this.placeOfBirth();
                if (pob) parts.push(pob);
                return parts.join(' · ');
            });
            this.coordinateLine = ko.computed(() => {
                const lat = this.latitude();
                const lng = this.longitude();
                if (lat == null || lng == null) return '';
                return `${Number(lat).toFixed(5)}, ${Number(lng).toFixed(5)}`;
            });

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

            this.locationSearchQuery.subscribe(() => {
                clearTimeout(this._locationSearchTimer);
                this._locationSearchTimer = setTimeout(() => this.runLocationSearch(), 400);
            });
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
                if (this._locationMap && this._locationMapMarker) {
                    const la = this.latitude();
                    const ln = this.longitude();
                    if (la != null && ln != null && !Number.isNaN(Number(la)) && !Number.isNaN(Number(ln))) {
                        this._locationMapMarker.setLatLng([Number(la), Number(ln)]);
                        this._locationMap.setView([Number(la), Number(ln)], 14);
                    }
                } else {
                    requestAnimationFrame(() => this.initLocationMap());
                }
            } catch (err) {
                console.error('Error loading profile:', err);
            } finally {
                this.loading(false);
            }
        };

        loadContent = async () => {
            const pid = this.personId();
            if (!pid) return;

            const [postsRes, photosRes, videosRes, docsRes, eventsRes, spousesRes] = await Promise.all([
                fetch(`/api/posts/person/${pid}`),
                fetch(`/api/photos/person/${pid}`),
                fetch(`/api/videos/person/${pid}`),
                fetch(`/api/documents/person/${pid}`),
                fetch(`/api/people/${pid}/events`),
                fetch(`/api/people/${pid}/spouses`)
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
            this.spouses(
                spouseList.map((s) => ({
                    spousePersonId: s.spousePersonId,
                    displayName: `${s.givenNames} ${s.familyName}`.trim(),
                    marriageYear: ko.observable(s.marriageYear ?? null),
                    marriageMonth: ko.observable(s.marriageMonth ?? null),
                    marriageDay: ko.observable(s.marriageDay ?? null),
                    divorceYear: ko.observable(s.divorceYear ?? null),
                    divorceMonth: ko.observable(s.divorceMonth ?? null),
                    divorceDay: ko.observable(s.divorceDay ?? null)
                }))
            );
        };

        saveProfile = async () => {
            const snapshot = this._personSnapshot || {};
            const body = {
                givenNames: this.givenNames(),
                familyName: this.familyName(),
                isMale: this.isMaleStr() === 'true',
                placeOfBirth: this.placeOfBirth() || null,
                placeOfDeath: snapshot.placeOfDeath ?? null,
                bio: this.bio() || null,
                latitude: this.latitude() != null ? Number(this.latitude()) : null,
                longitude: this.longitude() != null ? Number(this.longitude()) : null,
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

        numOrNull = (obs) => {
            const v = obs();
            if (v === '' || v === null || v === undefined) return null;
            const n = parseInt(v, 10);
            return Number.isNaN(n) ? null : n;
        };

        saveSpouseRow = async (row) => {
            const pid = this.personId();
            if (!pid) return;
            const body = {
                marriageYear: this.numOrNull(row.marriageYear),
                marriageMonth: this.numOrNull(row.marriageMonth),
                marriageDay: this.numOrNull(row.marriageDay),
                divorceYear: this.numOrNull(row.divorceYear),
                divorceMonth: this.numOrNull(row.divorceMonth),
                divorceDay: this.numOrNull(row.divorceDay)
            };
            try {
                const res = await fetch(`/api/people/${pid}/spouse/${row.spousePersonId}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) toast.success('Spouse dates saved.');
                else toast.error('Could not save spouse dates.');
            } catch {
                toast.error('Error saving spouse dates.');
            }
        };

        setMapPin = (lat, lng) => {
            this.latitude(lat);
            this.longitude(lng);
            if (this._locationMap && this._locationMapMarker) {
                this._locationMapMarker.setLatLng([lat, lng]);
                this._locationMap.panTo([lat, lng]);
            }
        };

        initLocationMap = () => {
            if (typeof L === 'undefined') return;
            const el = document.getElementById('profileLocationMap');
            if (!el || this._locationMap) return;

            const lat0 = this.latitude();
            const lng0 = this.longitude();
            const hasPos = lat0 != null && lng0 != null && !Number.isNaN(Number(lat0)) && !Number.isNaN(Number(lng0));
            const lat = hasPos ? Number(lat0) : -34.9285;
            const lng = hasPos ? Number(lng0) : 138.6007;

            this._locationMap = L.map('profileLocationMap').setView([lat, lng], hasPos ? 14 : 4);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(this._locationMap);

            this._locationMapMarker = L.marker([lat, lng], { draggable: true }).addTo(this._locationMap);
            this._locationMap.on('click', (e) => this.setMapPin(e.latlng.lat, e.latlng.lng));
            this._locationMapMarker.on('dragend', (e) => {
                const p = e.target.getLatLng();
                this.setMapPin(p.lat, p.lng);
            });

            setTimeout(() => this._locationMap?.invalidateSize(), 200);
        };

        runLocationSearch = async () => {
            const q = (this.locationSearchQuery() || '').trim();
            if (q.length < 3) {
                this.locationSearchResults([]);
                return;
            }
            try {
                const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=5`;
                const res = await fetch(url, { headers: { Accept: 'application/json' } });
                const data = await res.json();
                this.locationSearchResults(Array.isArray(data) ? data : []);
            } catch {
                this.locationSearchResults([]);
            }
        };

        pickLocationSearchResult = (place) => {
            const lat = parseFloat(place.lat);
            const lng = parseFloat(place.lon);
            if (Number.isNaN(lat) || Number.isNaN(lng)) return;
            this.locationSearchResults([]);
            this.locationSearchQuery(place.display_name || '');
            if (!this._locationMap) this.initLocationMap();
            this.setMapPin(lat, lng);
            this._locationMap?.setView([lat, lng], 15);
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
