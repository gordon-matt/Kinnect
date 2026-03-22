(function () {
    const explicitPersonId =
        typeof window.profilePersonId !== 'undefined' && window.profilePersonId !== null
            ? Number(window.profilePersonId)
            : null;

    // ── Shared helpers ─────────────────────────────────────────────────────────

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

    // Copyable event types (item 8)
    const COPYABLE_TYPES = new Set(['EMIG', 'IMMI', 'RESI']);

    class ProfileViewModel {
        constructor() {
            this.loading = ko.observable(true);
            this.personId = ko.observable(null);
            this.givenNames = ko.observable('');
            this.familyName = ko.observable('');
            this.isMaleStr = ko.observable('true');
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

            // posts (item 3 – paged)
            this.posts = ko.observableArray([]);
            this.postsCurrentPage = ko.observable(1);
            this.postsPageSize = 10;
            this.postsTotalCount = ko.observable(0);
            this.postsTotalPages = ko.observable(1);
            this.postsPageNumbers = ko.computed(() =>
                this._buildPageNumbers(this.postsCurrentPage(), this.postsTotalPages()));

            this.photos = ko.observableArray([]);
            this.videos = ko.observableArray([]);
            this.documents = ko.observableArray([]);
            this.events = ko.observableArray([]);
            this.spouses = ko.observableArray([]);

            // Merged timeline items (events + spouse relationship events) – item 1
            this.timelineItems = ko.computed(() => {
                const items = [];

                // Regular events (already ordered by service)
                for (const ev of this.events()) {
                    items.push({ ...ev, _source: 'event' });
                }

                // Synthetic spouse events (marriage, divorce, engagement) – item 1
                for (const sp of this.spouses()) {
                    const name = sp.displayName;
                    if (ko.unwrap(sp.engagementYear)) {
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'ENGA',
                            eventTypeLabel: 'Engagement',
                            year: ko.unwrap(sp.engagementYear),
                            month: ko.unwrap(sp.engagementMonth),
                            day: ko.unwrap(sp.engagementDay),
                            place: null,
                            description: `To ${name}`,
                            spousePersonId: sp.spousePersonId
                        });
                    }
                    if (ko.unwrap(sp.marriageYear)) {
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'MARR',
                            eventTypeLabel: 'Marriage',
                            year: ko.unwrap(sp.marriageYear),
                            month: ko.unwrap(sp.marriageMonth),
                            day: ko.unwrap(sp.marriageDay),
                            place: null,
                            description: `To ${name}`,
                            spousePersonId: sp.spousePersonId
                        });
                    }
                    if (ko.unwrap(sp.divorceYear)) {
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'DIV',
                            eventTypeLabel: 'Divorce',
                            year: ko.unwrap(sp.divorceYear),
                            month: ko.unwrap(sp.divorceMonth),
                            day: ko.unwrap(sp.divorceDay),
                            place: null,
                            description: `From ${name}`,
                            spousePersonId: sp.spousePersonId
                        });
                    }
                }

                // Sort all items chronologically
                return items.sort((a, b) => {
                    const ay = a.year ?? 9999, by = b.year ?? 9999;
                    if (ay !== by) return ay - by;
                    const am = a.month ?? 0, bm = b.month ?? 0;
                    if (am !== bm) return am - bm;
                    return (a.day ?? 0) - (b.day ?? 0);
                });
            });

            this.hasEventMapItems = ko.computed(() =>
                this.timelineItems().length > 0);

            this.fullName = ko.computed(() => `${this.givenNames()} ${this.familyName()}`);
            this.subtitleLine = ko.computed(() => {
                const birt = this.events().find((e) => e.eventType === 'BIRT');
                const parts = [];
                if (birt && birt.year != null) {
                    parts.push(
                        birt.month && birt.day
                            ? `b. ${birt.year}-${String(birt.month).padStart(2,'0')}-${String(birt.day).padStart(2,'0')}`
                            : `b. ${birt.year}`
                    );
                }
                if (birt && birt.place) parts.push(birt.place);
                return parts.join(' · ');
            });
            this.coordinateLine = ko.computed(() => {
                const lat = this.latitude();
                const lng = this.longitude();
                if (lat == null || lng == null) return '';
                return `${Number(lat).toFixed(5)}, ${Number(lng).toFixed(5)}`;
            });

            // Photo upload state
            this.isUploadingPhoto = ko.observable(false);
            this.photoTitle = ko.observable('');
            this.photoDescription = ko.observable('');
            this.photoYearTaken = ko.observable(null);
            this.photoMonthTaken = ko.observable(null);
            this.photoDayTaken = ko.observable(null);
            this.photoUploadDayOptions = daysInMonth(this.photoMonthTaken, this.photoYearTaken);
            this.photoSelectedEventIds = ko.observableArray([]);

            // Video upload state
            this.isUploadingVideo = ko.observable(false);
            this.videoTitle = ko.observable('');
            this.videoDescription = ko.observable('');
            this.videoSelectedEventIds = ko.observableArray([]);

            this.isUploadingDocument = ko.observable(false);
            this.documentTitle = ko.observable('');
            this.documentDescription = ko.observable('');

            // Add event form
            this.isAddingEvent = ko.observable(false);
            this.newEventType = ko.observable('BIRT');
            this.newEventYear = ko.observable(null);
            this.newEventMonth = ko.observable(null);
            this.newEventDay = ko.observable(null);
            this.newEventPlace = ko.observable('');
            this.newEventLatitude = ko.observable(null);
            this.newEventLongitude = ko.observable(null);
            this.newEventDescription = ko.observable('');
            this.newEventDayOptions = daysInMonth(this.newEventMonth, this.newEventYear);
            this._newEventMap = null;
            this._newEventMarker = null;

            // Edit event form
            this.editingEventId = ko.observable(null);
            this.editEventType = ko.observable('BIRT');
            this.editEventYear = ko.observable(null);
            this.editEventMonth = ko.observable(null);
            this.editEventDay = ko.observable(null);
            this.editEventPlace = ko.observable('');
            this.editEventLatitude = ko.observable(null);
            this.editEventLongitude = ko.observable(null);
            this.editEventDescription = ko.observable('');
            this.editEventDayOptions = daysInMonth(this.editEventMonth, this.editEventYear);
            this._editEventMap = null;
            this._editEventMarker = null;

            // Photo lightbox
            this.photoLightboxUrl = ko.observable(null);

            // Edit photo
            this.editingPhotoId = ko.observable(null);
            this.editPhotoTitle = ko.observable('');
            this.editPhotoDescription = ko.observable('');
            this.editPhotoYear = ko.observable(null);
            this.editPhotoMonth = ko.observable(null);
            this.editPhotoDay = ko.observable(null);
            this.editPhotoDayOptions = daysInMonth(this.editPhotoMonth, this.editPhotoYear);
            this.editPhotoTagsText = ko.observable('');
            this.editPhotoSelectedEventIds = ko.observableArray([]);

            // Copy event dialog (item 8)
            this.copyEventId = ko.observable(null);
            this.allPeople = ko.observableArray([]);
            this.copyTargetPersonId = ko.observable(null);

            this._explicitPersonId = explicitPersonId;
            this._tagifyInstances = {};
            this._personSnapshot = null;

            this.MONTHS = MONTHS;
            this.COPYABLE_TYPES = COPYABLE_TYPES;

            this.locationSearchQuery.subscribe(() => {
                clearTimeout(this._locationSearchTimer);
                this._locationSearchTimer = setTimeout(() => this.runLocationSearch(), 400);
            });
        }

        // ── Paging helpers ─────────────────────────────────────────────────────
        _buildPageNumbers(current, total) {
            const pages = [];
            let start = Math.max(1, current - 2);
            let end = Math.min(total, start + 4);
            start = Math.max(1, end - 4);
            for (let i = start; i <= end; i++) pages.push(i);
            return pages;
        }

        isCopyable = (eventType) => COPYABLE_TYPES.has(eventType);

        // ── Load ───────────────────────────────────────────────────────────────
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

            const [photosRes, videosRes, docsRes, eventsRes, spousesRes, peopleRes] = await Promise.all([
                fetch(`/api/photos/person/${pid}`),
                fetch(`/api/videos/person/${pid}`),
                fetch(`/api/documents/person/${pid}`),
                fetch(`/api/people/${pid}/events`),
                fetch(`/api/people/${pid}/spouses`),
                fetch('/api/people')
            ]);

            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();
            const eventsData = await eventsRes.json();
            const spousesData = await spousesRes.json();
            const peopleData = await peopleRes.json();

            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
            this.events(eventsData.value || eventsData || []);

            const rawPeople = peopleData.value || peopleData || [];
            this.allPeople(rawPeople.filter(p => p.id !== pid).map(p => ({
                id: p.id,
                fullName: p.fullName || `${p.givenNames} ${p.familyName}`
            })));

            const spouseList = spousesData.value || spousesData || [];
            this.spouses(spouseList.map((s) => {
                const row = {
                    spousePersonId: s.spousePersonId,
                    displayName: `${s.givenNames} ${s.familyName}`.trim(),
                    marriageYear: ko.observable(s.marriageYear ?? null),
                    marriageMonth: ko.observable(s.marriageMonth ?? null),
                    marriageDay: ko.observable(s.marriageDay ?? null),
                    divorceYear: ko.observable(s.divorceYear ?? null),
                    divorceMonth: ko.observable(s.divorceMonth ?? null),
                    divorceDay: ko.observable(s.divorceDay ?? null),
                    engagementYear: ko.observable(s.engagementYear ?? null),
                    engagementMonth: ko.observable(s.engagementMonth ?? null),
                    engagementDay: ko.observable(s.engagementDay ?? null)
                };
                row.marriageDayOptions = daysInMonth(row.marriageMonth, row.marriageYear);
                row.divorceDayOptions = daysInMonth(row.divorceMonth, row.divorceYear);
                row.engagementDayOptions = daysInMonth(row.engagementMonth, row.engagementYear);
                return row;
            }));

            await this.loadPostsPage(1);
        };

        // ── Posts paging (item 3) ──────────────────────────────────────────────
        loadPostsPage = async (page) => {
            const pid = this.personId();
            if (!pid) return;
            try {
                const res = await fetch(`/api/posts/person/${pid}/paged?page=${page}&pageSize=${this.postsPageSize}`);
                const data = await res.json();
                const paged = data.value || data;
                this.posts(paged.items || []);
                this.postsTotalCount(paged.totalCount || 0);
                const totalPages = Math.max(1, Math.ceil((paged.totalCount || 0) / this.postsPageSize));
                this.postsTotalPages(totalPages);
                this.postsCurrentPage(page);
            } catch (err) {
                console.error('Error loading posts:', err);
            }
        };

        postsGoToPage = async (page) => {
            if (page >= 1 && page <= this.postsTotalPages()) await this.loadPostsPage(page);
        };
        postsPreviousPage = async () => {
            if (this.postsCurrentPage() > 1) await this.loadPostsPage(this.postsCurrentPage() - 1);
        };
        postsNextPage = async () => {
            if (this.postsCurrentPage() < this.postsTotalPages()) await this.loadPostsPage(this.postsCurrentPage() + 1);
        };

        // ── Save profile ───────────────────────────────────────────────────────
        saveProfile = async () => {
            const snapshot = this._personSnapshot || {};
            const body = {
                givenNames: this.givenNames(),
                familyName: this.familyName(),
                isMale: this.isMaleStr() === 'true',
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

        // ── Spouse (item 4) ────────────────────────────────────────────────────
        saveSpouseRow = async (row) => {
            const pid = this.personId();
            if (!pid) return;
            const body = {
                marriageYear: this.numOrNull(row.marriageYear),
                marriageMonth: this.numOrNull(row.marriageMonth),
                marriageDay: this.numOrNull(row.marriageDay),
                divorceYear: this.numOrNull(row.divorceYear),
                divorceMonth: this.numOrNull(row.divorceMonth),
                divorceDay: this.numOrNull(row.divorceDay),
                engagementYear: this.numOrNull(row.engagementYear),
                engagementMonth: this.numOrNull(row.engagementMonth),
                engagementDay: this.numOrNull(row.engagementDay)
            };
            try {
                const res = await fetch(`/api/people/${pid}/spouse/${row.spousePersonId}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) {
                    toast.success('Spouse dates saved.');
                    // Re-render timeline items computed by re-fetching spouses
                    const spousesRes = await fetch(`/api/people/${pid}/spouses`);
                    const spousesData = await spousesRes.json();
                    const spouseList = spousesData.value || spousesData || [];
                    this.spouses(spouseList.map((s) => {
                        const r = {
                            spousePersonId: s.spousePersonId,
                            displayName: `${s.givenNames} ${s.familyName}`.trim(),
                            marriageYear: ko.observable(s.marriageYear ?? null),
                            marriageMonth: ko.observable(s.marriageMonth ?? null),
                            marriageDay: ko.observable(s.marriageDay ?? null),
                            divorceYear: ko.observable(s.divorceYear ?? null),
                            divorceMonth: ko.observable(s.divorceMonth ?? null),
                            divorceDay: ko.observable(s.divorceDay ?? null),
                            engagementYear: ko.observable(s.engagementYear ?? null),
                            engagementMonth: ko.observable(s.engagementMonth ?? null),
                            engagementDay: ko.observable(s.engagementDay ?? null)
                        };
                        r.marriageDayOptions = daysInMonth(r.marriageMonth, r.marriageYear);
                        r.divorceDayOptions = daysInMonth(r.divorceMonth, r.divorceYear);
                        r.engagementDayOptions = daysInMonth(r.engagementMonth, r.engagementYear);
                        return r;
                    }));
                } else {
                    toast.error('Could not save spouse dates.');
                }
            } catch {
                toast.error('Error saving spouse dates.');
            }
        };

        // ── Location map ───────────────────────────────────────────────────────
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
            if (q.length < 3) { this.locationSearchResults([]); return; }
            try {
                const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=5`;
                const res = await fetch(url, { headers: { Accept: 'application/json' } });
                const data = await res.json();
                this.locationSearchResults(Array.isArray(data) ? data : []);
            } catch { this.locationSearchResults([]); }
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

        // ── Event place mini-map (item 5) ──────────────────────────────────────
        _initEventMap = (mapId, latObs, lngObs, mapRef, markerRef) => {
            if (typeof L === 'undefined') return [mapRef, markerRef];
            const el = document.getElementById(mapId);
            if (!el) return [mapRef, markerRef];

            if (mapRef) { mapRef.remove(); }

            const lat0 = parseFloat(latObs()) || 0;
            const lng0 = parseFloat(lngObs()) || 0;
            const hasPos = lat0 !== 0 || lng0 !== 0;

            const m = L.map(mapId).setView(hasPos ? [lat0, lng0] : [20, 0], hasPos ? 12 : 2);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(m);

            const marker = L.marker(hasPos ? [lat0, lng0] : [20, 0], { draggable: true }).addTo(m);
            m.on('click', (e) => {
                latObs(e.latlng.lat);
                lngObs(e.latlng.lng);
                marker.setLatLng([e.latlng.lat, e.latlng.lng]);
            });
            marker.on('dragend', (e) => {
                const p = e.target.getLatLng();
                latObs(p.lat);
                lngObs(p.lng);
            });
            setTimeout(() => m.invalidateSize(), 200);
            return [m, marker];
        };

        initNewEventMap = () => {
            [this._newEventMap, this._newEventMarker] = this._initEventMap(
                'newEventMap', this.newEventLatitude, this.newEventLongitude,
                this._newEventMap, this._newEventMarker);
        };

        initEditEventMap = () => {
            [this._editEventMap, this._editEventMarker] = this._initEventMap(
                'editEventMap', this.editEventLatitude, this.editEventLongitude,
                this._editEventMap, this._editEventMarker);
        };

        // ── Profile image ──────────────────────────────────────────────────────
        uploadProfileImage = async (vm, event) => {
            const file = event.target.files[0];
            if (!file) return;
            const formData = new FormData();
            formData.append('file', file);
            try {
                const response = await fetch(`/api/people/${this.personId()}/profile-image`, {
                    method: 'POST', body: formData
                });
                if (response.ok) { await this.loadProfile(); toast.success('Profile image updated!'); }
                else toast.error('Failed to upload image.');
            } catch { toast.error('Error uploading image.'); }
        };

        // ── Timeline ───────────────────────────────────────────────────────────
        showAddEvent = () => {
            this.newEventType('BIRT');
            this.newEventYear(null);
            this.newEventMonth(null);
            this.newEventDay(null);
            this.newEventPlace('');
            this.newEventLatitude(null);
            this.newEventLongitude(null);
            this.newEventDescription('');
            this.isAddingEvent(true);
            // Init mini map after DOM renders
            setTimeout(() => this.initNewEventMap(), 150);
        };

        cancelAddEvent = () => {
            this.isAddingEvent(false);
            if (this._newEventMap) { this._newEventMap.remove(); this._newEventMap = null; }
        };

        addEvent = async () => {
            const pid = this.personId();
            if (!pid) return;

            const body = {
                eventType: this.newEventType(),
                year: this.newEventYear() || null,
                month: this.newEventMonth() || null,
                day: this.newEventDay() || null,
                place: this.newEventPlace() || null,
                latitude: this.newEventLatitude() != null ? Number(this.newEventLatitude()) : null,
                longitude: this.newEventLongitude() != null ? Number(this.newEventLongitude()) : null,
                description: this.newEventDescription() || null
            };

            try {
                const res = await fetch(`/api/people/${pid}/events`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });

                if (res.ok) {
                    this.cancelAddEvent();
                    await this.loadContent();
                    toast.success('Event added!');
                } else {
                    toast.error('Failed to add event.');
                }
            } catch { toast.error('Error adding event.'); }
        };

        startEditEvent = (ev) => {
            if (ev._source === 'spouse') {
                // Spouse events aren't editable here; redirect to profile section
                toast.info('Edit this date in the Spouse Relationships section above.');
                return;
            }
            this.editingEventId(ev.id);
            this.editEventType(ev.eventType);
            this.editEventYear(ev.year);
            this.editEventMonth(ev.month);
            this.editEventDay(ev.day);
            this.editEventPlace(ev.place || '');
            this.editEventLatitude(ev.latitude ?? null);
            this.editEventLongitude(ev.longitude ?? null);
            this.editEventDescription(ev.description || '');
            const el = document.getElementById('editEventModal');
            if (el) {
                bootstrap.Modal.getOrCreateInstance(el).show();
                el.addEventListener('shown.bs.modal', () => this.initEditEventMap(), { once: true });
            }
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
                latitude: this.editEventLatitude() != null ? Number(this.editEventLatitude()) : null,
                longitude: this.editEventLongitude() != null ? Number(this.editEventLongitude()) : null,
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
                } else { toast.error('Failed to update event.'); }
            } catch { toast.error('Error updating event.'); }
        };

        deleteEvent = async (eventId) => {
            const pid = this.personId();
            if (!confirm('Delete this event?')) return;
            try {
                const res = await fetch(`/api/people/${pid}/events/${eventId}`, { method: 'DELETE' });
                if (res.ok) { await this.loadContent(); toast.success('Event deleted.'); }
                else toast.error('Failed to delete event.');
            } catch { toast.error('Error deleting event.'); }
        };

        // ── Copy event (item 8) ────────────────────────────────────────────────
        openCopyEvent = (ev) => {
            this.copyEventId(ev.id);
            this.copyTargetPersonId(null);
            const el = document.getElementById('copyEventModal');
            if (el) bootstrap.Modal.getOrCreateInstance(el).show();
        };

        copyEventToPerson = async () => {
            const eid = this.copyEventId();
            const targetId = this.copyTargetPersonId();
            if (!eid || !targetId) { toast.error('Please select a person.'); return; }

            try {
                const res = await fetch(`/api/people/${this.personId()}/events/${eid}/copy/${targetId}`, {
                    method: 'POST'
                });
                if (res.ok) {
                    bootstrap.Modal.getInstance(document.getElementById('copyEventModal'))?.hide();
                    toast.success('Event copied!');
                } else { toast.error('Failed to copy event.'); }
            } catch { toast.error('Error copying event.'); }
        };

        formatDateTime(dateStr) {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            return `${date.toLocaleDateString()} at ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
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

        // ── Photos ─────────────────────────────────────────────────────────────
        formatPhotoDate = (photo) => {
            if (photo == null || photo.yearTaken == null) return '';
            const y = photo.yearTaken;
            if (photo.monthTaken != null && photo.dayTaken != null)
                return `${y}-${String(photo.monthTaken).padStart(2,'0')}-${String(photo.dayTaken).padStart(2,'0')}`;
            if (photo.monthTaken != null) return `${y}-${String(photo.monthTaken).padStart(2,'0')}`;
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
            this.editPhotoSelectedEventIds(photo.eventIds || []);
            const el = document.getElementById('editPhotoModal');
            if (el) bootstrap.Modal.getOrCreateInstance(el).show();
        };

        togglePhotoEvent = (eventId) => {
            const ids = this.editPhotoSelectedEventIds();
            const idx = ids.indexOf(eventId);
            if (idx >= 0) this.editPhotoSelectedEventIds(ids.filter(i => i !== eventId));
            else this.editPhotoSelectedEventIds([...ids, eventId]);
        };

        toggleUploadPhotoEvent = (eventId) => {
            const ids = this.photoSelectedEventIds();
            const idx = ids.indexOf(eventId);
            if (idx >= 0) this.photoSelectedEventIds(ids.filter(i => i !== eventId));
            else this.photoSelectedEventIds([...ids, eventId]);
        };

        isPhotoEventSelected = (eventId) =>
            ko.computed(() => this.editPhotoSelectedEventIds().includes(eventId));

        isUploadPhotoEventSelected = (eventId) =>
            ko.computed(() => this.photoSelectedEventIds().includes(eventId));

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
                tags,
                eventIds: this.editPhotoSelectedEventIds()
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
                } else toast.error('Failed to update photo.');
            } catch { toast.error('Error updating photo.'); }
        };

        // ── Media uploads ──────────────────────────────────────────────────────
        showPhotoUpload = () => {
            this.isUploadingPhoto(true);
            this.photoYearTaken(null);
            this.photoMonthTaken(null);
            this.photoDayTaken(null);
            this.photoSelectedEventIds([]);
            this.initTagify('photoTags');
        };
        cancelPhotoUpload = () => this.isUploadingPhoto(false);

        showVideoUpload = () => {
            this.isUploadingVideo(true);
            this.videoSelectedEventIds([]);
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
                const whitelist = (tagsResult.value || tagsResult || []).map(t => t.name);
                this._tagifyInstances[inputId] = new Tagify(input, {
                    whitelist, enforceWhitelist: false, maxTags: 20,
                    dropdown: { maxItems: 20, enabled: 1, closeOnSelect: false }
                });
            }, 100);
        };

        getTagValues = (inputId) => {
            const tagify = this._tagifyInstances[inputId];
            return tagify ? tagify.value.map(t => t.value) : [];
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
                this.photoSelectedEventIds([]);
                await this.loadContent();
                toast.success('Photo uploaded!');
            } else toast.error('Failed to upload photo.');
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
                this.videoSelectedEventIds([]);
                await this.loadContent();
                toast.success('Video uploaded!');
            } else toast.error('Failed to upload video.');
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
            } else toast.error('Failed to upload document.');
        };
    }

    document.addEventListener('DOMContentLoaded', async () => {
        const vm = new ProfileViewModel();
        ko.applyBindings(vm);
        await vm.loadProfile();
    });
})();
