(function () {
    const TAGGED_FOLDER_KEY = '__tagged__';
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
    const SINGLE_INSTANCE_EVENT_TYPES = new Set(['BIRT', 'DEAT', 'CHR', 'BURI', 'CREM']);
    const TIMELINE_EVENT_TYPE_ORDER = ['BIRT', 'DEAT', 'BAPM', 'CHR', 'CONF', 'ADOP', 'EMIG', 'IMMI', 'NATU', 'MARB', 'MARL', 'BURI', 'CREM', 'RESI', 'EVEN'];

    /** Matches server PersonEventDto.DateDisplay logic for spouse-synthetic rows */
    function partialDateDisplay(year, month, day) {
        if (year == null) return null;
        if (month != null && day != null)
            return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
        return String(year);
    }

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
            this.isDeceased = ko.observable(false);

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
            this.mediaFolders = ko.observableArray([]);
            this.currentMediaFolderId = ko.observable(null);
            this.currentUserPersonId = ko.observable(null);
            this.isAdminUser = !!window.profileIsAdmin;
            this.events = ko.observableArray([]);
            this.spouses = ko.observableArray([]);

            this.inMediaFolder = ko.computed(() => this.currentMediaFolderId() != null);
            this.ownedPhotos = ko.computed(() => this.photos().filter(p => this.isProfileOwnedMedia(p)));
            this.ownedVideos = ko.computed(() => this.videos().filter(v => this.isProfileOwnedMedia(v)));
            this.taggedPhotos = ko.computed(() => this.photos().filter(p => !this.isProfileOwnedMedia(p)));
            this.taggedVideos = ko.computed(() => this.videos().filter(v => !this.isProfileOwnedMedia(v)));
            this.hasTaggedMedia = ko.computed(() =>
                this.photos().some(p => !this.isProfileOwnedMedia(p)) ||
                this.videos().some(v => !this.isProfileOwnedMedia(v)));
            this.currentMediaFolderName = ko.computed(() => {
                const id = this.currentMediaFolderId();
                if (id == null) return '';
                if (id === TAGGED_FOLDER_KEY) return 'Tagged';
                const folder = this.mediaFolders().find(f => f.id === id);
                return folder?.name || '';
            });
            this.visiblePhotos = ko.computed(() => {
                const folderId = this.currentMediaFolderId();
                if (folderId == null) return this.ownedPhotos().filter(p => p.folderId == null);
                if (folderId === TAGGED_FOLDER_KEY) return this.taggedPhotos();
                return this.ownedPhotos().filter(p => p.folderId === folderId);
            });
            this.visibleVideos = ko.computed(() => {
                const folderId = this.currentMediaFolderId();
                if (folderId == null) return this.ownedVideos().filter(v => v.folderId == null);
                if (folderId === TAGGED_FOLDER_KEY) return this.taggedVideos();
                return this.ownedVideos().filter(v => v.folderId === folderId);
            });

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
                        const y = ko.unwrap(sp.engagementYear);
                        const mo = ko.unwrap(sp.engagementMonth);
                        const d = ko.unwrap(sp.engagementDay);
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'ENGA',
                            eventTypeLabel: 'Engagement',
                            year: y,
                            month: mo,
                            day: d,
                            dateDisplay: partialDateDisplay(y, mo, d),
                            place: null,
                            description: `To ${name}`,
                            spousePersonId: sp.spousePersonId
                        });
                    }
                    if (ko.unwrap(sp.marriageYear)) {
                        const y = ko.unwrap(sp.marriageYear);
                        const mo = ko.unwrap(sp.marriageMonth);
                        const d = ko.unwrap(sp.marriageDay);
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'MARR',
                            eventTypeLabel: 'Marriage',
                            year: y,
                            month: mo,
                            day: d,
                            dateDisplay: partialDateDisplay(y, mo, d),
                            place: null,
                            description: `To ${name}`,
                            spousePersonId: sp.spousePersonId
                        });
                    }
                    if (ko.unwrap(sp.divorceYear)) {
                        const y = ko.unwrap(sp.divorceYear);
                        const mo = ko.unwrap(sp.divorceMonth);
                        const d = ko.unwrap(sp.divorceDay);
                        items.push({
                            _source: 'spouse',
                            id: null,
                            eventType: 'DIV',
                            eventTypeLabel: 'Divorce',
                            year: y,
                            month: mo,
                            day: d,
                            dateDisplay: partialDateDisplay(y, mo, d),
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
            this.photoFolderId = ko.observable(null);

            // Video upload state
            this.isUploadingVideo = ko.observable(false);
            this.videoTitle = ko.observable('');
            this.videoDescription = ko.observable('');
            this.videoSelectedEventIds = ko.observableArray([]);
            this.videoFolderId = ko.observable(null);

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
            this.newEventLocationSearchQuery = ko.observable('');
            this.newEventLocationSearchResults = ko.observableArray([]);
            this._newEventLocationSearchTimer = null;
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
            this.editEventLocationSearchQuery = ko.observable('');
            this.editEventLocationSearchResults = ko.observableArray([]);
            this._editEventLocationSearchTimer = null;
            this.editEventDayOptions = daysInMonth(this.editEventMonth, this.editEventYear);
            this._editEventMap = null;
            this._editEventMarker = null;

            // Photo lightbox
            this.photoLightboxUrl = ko.observable(null);
            this.lightboxPhotoId = ko.observable(null);
            this.lightboxPhotoTitle = ko.observable('');
            this.lightboxPhotoDate = ko.observable('');
            this.lightboxCanEdit = ko.observable(false);
            this.lightboxHasAnnotations = ko.observable(false);
            this.lightboxShowAnnotations = ko.observable(true);
            this.lightboxTaggedPeople = ko.observableArray([]);
            this.lightboxPersonTagQuery = ko.observable('');
            this.lightboxPersonTagResults = ko.observableArray([]);
            this._lightboxAnno = null;
            this._lightboxPersonTagTimer = null;
            this.lightboxSelectedAnnotationId = ko.observable(null);
            this._annotationHoverLabelEl = null;
            this._lastMouseX = 0;
            this._lastMouseY = 0;

            this.lightboxPersonTagQuery.subscribe(() => {
                clearTimeout(this._lightboxPersonTagTimer);
                this._lightboxPersonTagTimer = setTimeout(() => this._runLightboxPersonTagSearch(), 250);
            });
            this.lightboxShowAnnotations.subscribe(() => {
                if (!this.lightboxCanEdit()) this.initLightboxAnnotorious();
            });

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
            this.editPhotoFolderId = ko.observable(null);

            // Edit photo location (GPS coordinates)
            this.editPhotoLatitude = ko.observable(null);
            this.editPhotoLongitude = ko.observable(null);
            // Set when modal opens, so the edit UI doesn't disappear mid-edit after the user sets coords.
            this.editPhotoLocationLockedByExif = ko.observable(false);
            this.editPhotoCanEditLocation = ko.computed(() => !this.editPhotoLocationLockedByExif());
            this.editPhotoLocationSearchQuery = ko.observable('');
            this.editPhotoLocationSearchResults = ko.observableArray([]);
            this._editPhotoLocationSearchTimer = null;
            this._editPhotoLocationMap = null;
            this._editPhotoLocationMarker = null;

            // Edit video
            this.editingVideoId = ko.observable(null);
            this.editVideoTitle = ko.observable('');
            this.editVideoDescription = ko.observable('');
            this.editVideoTagsText = ko.observable('');
            this.editVideoFolderId = ko.observable(null);

            // Folder creation
            this.isAddingFolder = ko.observable(false);
            this.newFolderName = ko.observable('');
            this.newFolderDescription = ko.observable('');

            // Copy event dialog (item 8)
            this.copyEventId = ko.observable(null);
            this.allPeople = ko.observableArray([]);
            this.copyTargetPersonId = ko.observable(null);

            this._explicitPersonId = explicitPersonId;
            this._tagifyInstances = {};
            this._personSnapshot = null;

            this.MONTHS = MONTHS;
            this.COPYABLE_TYPES = COPYABLE_TYPES;
            this.timelineEventOptions = [
                { type: 'BIRT', label: 'Birth' },
                { type: 'DEAT', label: 'Death' },
                { type: 'BAPM', label: 'Baptism' },
                { type: 'CHR', label: 'Christening' },
                { type: 'CONF', label: 'Confirmation' },
                { type: 'ADOP', label: 'Adoption' },
                { type: 'EMIG', label: 'Emigration' },
                { type: 'IMMI', label: 'Immigration' },
                { type: 'NATU', label: 'Naturalization' },
                { type: 'MARB', label: 'Marriage Banns' },
                { type: 'MARL', label: 'Marriage License' },
                { type: 'BURI', label: 'Burial' },
                { type: 'CREM', label: 'Cremation' },
                { type: 'RESI', label: 'Residence' },
                { type: 'EVEN', label: 'Custom Event' }
            ];

            this.locationSearchQuery.subscribe(() => {
                clearTimeout(this._locationSearchTimer);
                this._locationSearchTimer = setTimeout(() => this.runLocationSearch(), 400);
            });
            this.newEventLocationSearchQuery.subscribe(() => {
                clearTimeout(this._newEventLocationSearchTimer);
                this._newEventLocationSearchTimer = setTimeout(() => this.runNewEventLocationSearch(), 400);
            });
            this.editEventLocationSearchQuery.subscribe(() => {
                clearTimeout(this._editEventLocationSearchTimer);
                this._editEventLocationSearchTimer = setTimeout(() => this.runEditEventLocationSearch(), 400);
            });

            this.editPhotoLocationSearchQuery.subscribe(() => {
                clearTimeout(this._editPhotoLocationSearchTimer);
                this._editPhotoLocationSearchTimer = setTimeout(() => this.runEditPhotoLocationSearch(), 400);
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
        isSingleInstanceEventType = (eventType) => SINGLE_INSTANCE_EVENT_TYPES.has(String(eventType || '').toUpperCase());

        hasTimelineEventType = (eventType, excludingEventId = null) => {
            const normalizedType = String(eventType || '').toUpperCase();
            return this.events().some(e => e.eventType === normalizedType && (excludingEventId == null || e.id !== excludingEventId));
        };

        canUseTimelineEventType = (eventType, excludingEventId = null) => {
            if (!this.isSingleInstanceEventType(eventType)) return true;
            return !this.hasTimelineEventType(eventType, excludingEventId);
        };

        isTimelineOptionDisabled = (eventType, excludingEventId = null) =>
            !this.canUseTimelineEventType(eventType, excludingEventId);

        timelineEventTypeLabel = (eventType) => {
            const normalized = String(eventType || '').toUpperCase();
            const opt = this.timelineEventOptions.find((o) => o.type === normalized);
            return opt?.label ?? normalized;
        };

        firstAvailableTimelineEventType = (preferredType = 'BIRT', excludingEventId = null) => {
            const normalizedPreferred = String(preferredType || '').toUpperCase();
            if (this.canUseTimelineEventType(normalizedPreferred, excludingEventId)) {
                return normalizedPreferred;
            }
            const fallback = TIMELINE_EVENT_TYPE_ORDER.find(t => this.canUseTimelineEventType(t, excludingEventId));
            return fallback || 'EVEN';
        };

        readApiError = async (response, fallbackMessage) => {
            try {
                const payload = await response.json();
                if (typeof payload === 'string' && payload.trim()) return payload;
                const errors = payload?.errors;
                if (Array.isArray(errors) && errors.length > 0) {
                    const firstError = errors[0];
                    if (typeof firstError === 'string' && firstError.trim()) return firstError;
                    if (typeof firstError?.errorMessage === 'string' && firstError.errorMessage.trim()) return firstError.errorMessage;
                    if (typeof firstError?.message === 'string' && firstError.message.trim()) return firstError.message;
                }
                if (Array.isArray(payload?.validationErrors) && payload.validationErrors.length > 0) {
                    const firstValidationError = payload.validationErrors[0];
                    if (typeof firstValidationError === 'string' && firstValidationError.trim()) return firstValidationError;
                    if (typeof firstValidationError?.errorMessage === 'string' && firstValidationError.errorMessage.trim()) return firstValidationError.errorMessage;
                }
                if (typeof payload?.error === 'string' && payload.error.trim()) return payload.error;
                if (typeof payload?.message === 'string' && payload.message.trim()) return payload.message;
            } catch {
                // Ignore parse errors and return fallback below.
            }

            return fallbackMessage;
        };

        // ── Load ───────────────────────────────────────────────────────────────
        loadProfile = async () => {
            try {
                const url = this._explicitPersonId != null ? `/api/people/${this._explicitPersonId}` : '/api/people/me';
                const response = await fetch(url);
                const result = await response.json();
                const person = result.value || result;
                this._personSnapshot = person;

                this.personId(person.id);
                if (this._explicitPersonId == null) {
                    this.currentUserPersonId(person.id);
                } else {
                    try {
                        const meRes = await fetch('/api/people/me');
                        if (meRes.ok) {
                            const meResult = await meRes.json();
                            const me = meResult.value || meResult;
                            this.currentUserPersonId(me.id ?? null);
                        }
                    } catch {
                        this.currentUserPersonId(null);
                    }
                }
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
                this.isDeceased(!!person.isDeceased);

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

            const [photosRes, videosRes, docsRes, eventsRes, spousesRes, peopleRes, foldersRes] = await Promise.all([
                fetch(`/api/photos/person/${pid}`),
                fetch(`/api/videos/person/${pid}`),
                fetch(`/api/documents/person/${pid}`),
                fetch(`/api/people/${pid}/events`),
                fetch(`/api/people/${pid}/spouses`),
                fetch('/api/people'),
                fetch(`/api/media-folders/person/${pid}`)
            ]);

            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();
            const eventsData = await eventsRes.json();
            const spousesData = await spousesRes.json();
            const peopleData = await peopleRes.json();
            const foldersData = await foldersRes.json();

            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
            this.events(eventsData.value || eventsData || []);
            this.mediaFolders(foldersData.value || foldersData || []);

            const rawPeople = peopleData.value || peopleData || [];
            this.allPeople(rawPeople.map(p => ({
                id: p.id,
                fullName: p.fullName || `${p.givenNames} ${p.familyName}`
            })));

            const spouseList = spousesData.value || spousesData || [];
            this.spouses(spouseList.map((s) => {
                const row = {
                    spousePersonId: s.spousePersonId,
                    displayName: `${s.givenNames} ${s.familyName}`.trim(),
                    hasEngagement: ko.observable(s.hasEngagement ?? false),
                    hasMarriage: ko.observable(s.hasMarriage ?? false),
                    hasDivorce: ko.observable(s.hasDivorce ?? false),
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
                note: this.note() || null,
                isDeceased: this.isDeceased()
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
            const hasEnga = ko.unwrap(row.hasEngagement);
            const hasMarr = ko.unwrap(row.hasMarriage);
            const hasDiv  = ko.unwrap(row.hasDivorce);
            const body = {
                hasEngagement: hasEnga,
                engagementYear: hasEnga ? this.numOrNull(row.engagementYear) : null,
                engagementMonth: hasEnga ? this.numOrNull(row.engagementMonth) : null,
                engagementDay: hasEnga ? this.numOrNull(row.engagementDay) : null,
                hasMarriage: hasMarr,
                marriageYear: hasMarr ? this.numOrNull(row.marriageYear) : null,
                marriageMonth: hasMarr ? this.numOrNull(row.marriageMonth) : null,
                marriageDay: hasMarr ? this.numOrNull(row.marriageDay) : null,
                hasDivorce: hasDiv,
                divorceYear: hasDiv ? this.numOrNull(row.divorceYear) : null,
                divorceMonth: hasDiv ? this.numOrNull(row.divorceMonth) : null,
                divorceDay: hasDiv ? this.numOrNull(row.divorceDay) : null
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
                            hasEngagement: ko.observable(s.hasEngagement ?? false),
                            hasMarriage: ko.observable(s.hasMarriage ?? false),
                            hasDivorce: ko.observable(s.hasDivorce ?? false),
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

        runNewEventLocationSearch = async () => {
            const q = (this.newEventLocationSearchQuery() || '').trim();
            if (q.length < 3) {
                this.newEventLocationSearchResults([]);
                return;
            }
            try {
                const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=5`;
                const res = await fetch(url, { headers: { Accept: 'application/json' } });
                const data = await res.json();
                this.newEventLocationSearchResults(Array.isArray(data) ? data : []);
            } catch {
                this.newEventLocationSearchResults([]);
            }
        };

        pickNewEventLocationResult = (place) => {
            const lat = parseFloat(place.lat);
            const lng = parseFloat(place.lon);
            if (Number.isNaN(lat) || Number.isNaN(lng)) return;
            this.newEventLocationSearchResults([]);
            const name = place.display_name || '';
            this.newEventLocationSearchQuery(name);
            this.newEventPlace(name);
            if (!this._newEventMap) this.initNewEventMap();
            this.setNewEventPin(lat, lng);
            this._newEventMap?.setView([lat, lng], 15);
        };

        setNewEventPin = (lat, lng) => {
            this.newEventLatitude(lat);
            this.newEventLongitude(lng);
            if (!this._newEventMap) return;
            if (!this._newEventMarker) {
                this._newEventMarker = L.marker([lat, lng], { draggable: true }).addTo(this._newEventMap);
                this._newEventMarker.on('dragend', (e) => {
                    const p = e.target.getLatLng();
                    this.newEventLatitude(p.lat);
                    this.newEventLongitude(p.lng);
                });
            } else {
                this._newEventMarker.setLatLng([lat, lng]);
            }
            this._newEventMap.setView([lat, lng], 14);
        };

        clearNewEventPlace = (d, e) => {
            if (e) e.preventDefault();
            this.newEventPlace('');
            this.newEventLatitude(null);
            this.newEventLongitude(null);
            this.newEventLocationSearchQuery('');
            this.newEventLocationSearchResults([]);
            if (this._newEventMap && this._newEventMarker) {
                this._newEventMap.removeLayer(this._newEventMarker);
                this._newEventMarker = null;
            }
        };

        runEditEventLocationSearch = async () => {
            const q = (this.editEventLocationSearchQuery() || '').trim();
            if (q.length < 3) {
                this.editEventLocationSearchResults([]);
                return;
            }
            try {
                const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=5`;
                const res = await fetch(url, { headers: { Accept: 'application/json' } });
                const data = await res.json();
                this.editEventLocationSearchResults(Array.isArray(data) ? data : []);
            } catch {
                this.editEventLocationSearchResults([]);
            }
        };

        pickEditEventLocationResult = (place) => {
            const lat = parseFloat(place.lat);
            const lng = parseFloat(place.lon);
            if (Number.isNaN(lat) || Number.isNaN(lng)) return;
            this.editEventLocationSearchResults([]);
            const name = place.display_name || '';
            this.editEventLocationSearchQuery(name);
            this.editEventPlace(name);
            if (!this._editEventMap) this.initEditEventMap();
            this.setEditEventPin(lat, lng);
            this._editEventMap?.setView([lat, lng], 15);
        };

        setEditEventPin = (lat, lng) => {
            this.editEventLatitude(lat);
            this.editEventLongitude(lng);
            if (!this._editEventMap) return;
            if (!this._editEventMarker) {
                this._editEventMarker = L.marker([lat, lng], { draggable: true }).addTo(this._editEventMap);
                this._editEventMarker.on('dragend', (e) => {
                    const p = e.target.getLatLng();
                    this.editEventLatitude(p.lat);
                    this.editEventLongitude(p.lng);
                });
            } else {
                this._editEventMarker.setLatLng([lat, lng]);
            }
            this._editEventMap.setView([lat, lng], 14);
        };

        clearEditEventPlace = (d, e) => {
            if (e) e.preventDefault();
            this.editEventPlace('');
            this.editEventLatitude(null);
            this.editEventLongitude(null);
            this.editEventLocationSearchQuery('');
            this.editEventLocationSearchResults([]);
            if (this._editEventMap && this._editEventMarker) {
                this._editEventMap.removeLayer(this._editEventMarker);
                this._editEventMarker = null;
            }
        };

        initNewEventMap = () => {
            if (typeof L === 'undefined') return;
            const el = document.getElementById('newEventMap');
            if (!el) return;
            if (this._newEventMap) {
                this._newEventMap.remove();
                this._newEventMap = null;
            }
            this._newEventMarker = null;

            const lat0 = this.newEventLatitude();
            const lng0 = this.newEventLongitude();
            const hasPos =
                lat0 != null &&
                lng0 != null &&
                !Number.isNaN(Number(lat0)) &&
                !Number.isNaN(Number(lng0));

            const m = L.map('newEventMap').setView(hasPos ? [Number(lat0), Number(lng0)] : [20, 0], hasPos ? 14 : 2);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(m);
            this._newEventMap = m;
            m.on('click', (e) => this.setNewEventPin(e.latlng.lat, e.latlng.lng));
            if (hasPos) this.setNewEventPin(Number(lat0), Number(lng0));
            setTimeout(() => m.invalidateSize(), 200);
        };

        initEditEventMap = () => {
            if (typeof L === 'undefined') return;
            const el = document.getElementById('editEventMap');
            if (!el) return;
            if (this._editEventMap) {
                this._editEventMap.remove();
                this._editEventMap = null;
            }
            this._editEventMarker = null;

            const lat0 = this.editEventLatitude();
            const lng0 = this.editEventLongitude();
            const hasPos =
                lat0 != null &&
                lng0 != null &&
                !Number.isNaN(Number(lat0)) &&
                !Number.isNaN(Number(lng0));

            const m = L.map('editEventMap').setView(hasPos ? [Number(lat0), Number(lng0)] : [20, 0], hasPos ? 14 : 2);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(m);
            this._editEventMap = m;
            m.on('click', (e) => this.setEditEventPin(e.latlng.lat, e.latlng.lng));
            if (hasPos) this.setEditEventPin(Number(lat0), Number(lng0));
            setTimeout(() => m.invalidateSize(), 200);
        };

        // ── Edit photo location ─────────────────────────────────────────────
        setEditPhotoLocationPin = (lat, lng) => {
            this.editPhotoLatitude(lat);
            this.editPhotoLongitude(lng);
            if (this._editPhotoLocationMap && this._editPhotoLocationMarker) {
                this._editPhotoLocationMarker.setLatLng([lat, lng]);
                this._editPhotoLocationMap.panTo([lat, lng]);
            }
        };

        runEditPhotoLocationSearch = async () => {
            if (!this.editPhotoCanEditLocation()) {
                this.editPhotoLocationSearchResults([]);
                return;
            }

            const q = (this.editPhotoLocationSearchQuery() || '').trim();
            if (q.length < 3) {
                this.editPhotoLocationSearchResults([]);
                return;
            }

            try {
                const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(q)}&limit=5`;
                const res = await fetch(url, { headers: { Accept: 'application/json' } });
                const data = await res.json();
                this.editPhotoLocationSearchResults(Array.isArray(data) ? data : []);
            } catch {
                this.editPhotoLocationSearchResults([]);
            }
        };

        pickEditPhotoLocationSearchResult = (place) => {
            if (!place || !this.editPhotoCanEditLocation()) return;

            const lat = parseFloat(place.lat);
            const lng = parseFloat(place.lon);
            if (Number.isNaN(lat) || Number.isNaN(lng)) return;

            this.editPhotoLocationSearchResults([]);
            this.editPhotoLocationSearchQuery(place.display_name || '');

            if (!this._editPhotoLocationMap) this.initEditPhotoLocationMap();
            this.setEditPhotoLocationPin(lat, lng);
            this._editPhotoLocationMap?.setView([lat, lng], 15);
        };

        initEditPhotoLocationMap = () => {
            if (typeof L === 'undefined') return;
            const el = document.getElementById('editPhotoLocationMap');
            if (!el) return;
            if (this._editPhotoLocationMap) return;

            const lat0 = this.editPhotoLatitude();
            const lng0 = this.editPhotoLongitude();
            const hasPos =
                lat0 != null &&
                lng0 != null &&
                !Number.isNaN(Number(lat0)) &&
                !Number.isNaN(Number(lng0));

            const lat = hasPos ? Number(lat0) : -34.9285;
            const lng = hasPos ? Number(lng0) : 138.6007;

            const canEdit = this.editPhotoCanEditLocation();
            this._editPhotoLocationMap = L.map('editPhotoLocationMap').setView(
                [lat, lng],
                hasPos ? 14 : 4
            );

            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; OpenStreetMap contributors',
                maxZoom: 19
            }).addTo(this._editPhotoLocationMap);

            this._editPhotoLocationMarker = L.marker([lat, lng], { draggable: canEdit }).addTo(this._editPhotoLocationMap);

            if (canEdit) {
                this._editPhotoLocationMap.on('click', (e) => this.setEditPhotoLocationPin(e.latlng.lat, e.latlng.lng));
                this._editPhotoLocationMarker.on('dragend', (e) => {
                    const p = e.target.getLatLng();
                    this.setEditPhotoLocationPin(p.lat, p.lng);
                });
            }

            setTimeout(() => this._editPhotoLocationMap?.invalidateSize(), 200);
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
            this.newEventType(this.firstAvailableTimelineEventType('BIRT'));
            this.newEventYear(null);
            this.newEventMonth(null);
            this.newEventDay(null);
            this.newEventPlace('');
            this.newEventLatitude(null);
            this.newEventLongitude(null);
            this.newEventDescription('');
            this.newEventLocationSearchQuery('');
            this.newEventLocationSearchResults([]);
            this.isAddingEvent(true);
            setTimeout(() => this.initNewEventMap(), 150);
        };

        cancelAddEvent = () => {
            this.isAddingEvent(false);
            this.newEventLocationSearchQuery('');
            this.newEventLocationSearchResults([]);
            if (this._newEventMap) {
                this._newEventMap.remove();
                this._newEventMap = null;
            }
            this._newEventMarker = null;
        };

        addEvent = async () => {
            const pid = this.personId();
            if (!pid) return;
            const eventType = String(this.newEventType() || '').toUpperCase();

            if (!this.canUseTimelineEventType(eventType)) {
                toast.error(`Only one ${this.timelineEventTypeLabel(eventType)} event is allowed.`);
                return;
            }

            const body = {
                eventType,
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
                    // Auto-mark deceased when a death event with a year is recorded
                    if (body.eventType === 'DEAT' && body.year && !this.isDeceased()) {
                        this.isDeceased(true);
                        toast.info('Person has been automatically marked as deceased.');
                    }
                } else {
                    toast.error(await this.readApiError(res, 'Failed to add event.'));
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
            this.editEventLocationSearchQuery(ev.place || '');
            this.editEventLocationSearchResults([]);
            const el = document.getElementById('editEventModal');
            if (el) bootstrap.Modal.getOrCreateInstance(el).show();
        };

        saveEditEvent = async () => {
            const pid = this.personId();
            const eid = this.editingEventId();
            if (!pid || !eid) return;
            const eventType = String(this.editEventType() || '').toUpperCase();

            if (!this.canUseTimelineEventType(eventType, eid)) {
                toast.error('That event type already exists and can only be used once.');
                return;
            }

            const body = {
                eventType,
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
                    // Auto-mark deceased when a death event with a year is recorded
                    if (body.eventType === 'DEAT' && body.year && !this.isDeceased()) {
                        this.isDeceased(true);
                        toast.info('Person has been automatically marked as deceased.');
                    }
                } else { toast.error(await this.readApiError(res, 'Failed to update event.')); }
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
                } else { toast.error(await this.readApiError(res, 'Failed to copy event.')); }
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

        openPhotoLightbox = async (photo) => {
            this.photoLightboxUrl('/uploads/' + photo.filePath);
            this.lightboxPhotoId(photo.id);
            this.lightboxPhotoTitle(photo.title || '');
            this.lightboxPhotoDate(this.formatPhotoDate(photo));
            this.lightboxCanEdit(this.canEditMedia(photo));
            this.lightboxHasAnnotations(!!photo.annotationsJson);
            this.lightboxShowAnnotations(true);
            this.lightboxTaggedPeople(photo.taggedPeople || []);
            this.lightboxPersonTagQuery('');
            this.lightboxPersonTagResults([]);

            const el = document.getElementById('photoLightboxModal');
            if (el) bootstrap.Modal.getOrCreateInstance(el).show();

            // Refresh full photo data (includes annotationsJson and taggedPeople)
            try {
                const res = await fetch(`/api/photos/${photo.id}`);
                if (res.ok) {
                    const data = await res.json();
                    const full = data.value || data;
                    this.lightboxTaggedPeople(full.taggedPeople || []);
                    this.lightboxHasAnnotations(!!full.annotationsJson);
                    this._lightboxPhotoData = full;
                }
            } catch { /* non-fatal */ }
        };

        _runLightboxPersonTagSearch = () => {
            const q = this.lightboxPersonTagQuery().replace(/^#/, '').toLowerCase().trim();
            if (q.length < 1) { this.lightboxPersonTagResults([]); return; }
            const taggedIds = new Set(this.lightboxTaggedPeople().map(p => p.personId));
            const results = this.allPeople()
                .filter(p => !taggedIds.has(p.id) && p.fullName.toLowerCase().includes(q))
                .slice(0, 6);
            this.lightboxPersonTagResults(results);
        };

        tagPersonFromLightbox = async (person) => {
            if (!this.lightboxCanEdit()) return;
            const pid = this.lightboxPhotoId();
            if (!pid) return;
            try {
                const res = await fetch(`/api/photos/${pid}/tag-person/${person.id}`, { method: 'POST' });
                if (res.ok) {
                    this.lightboxTaggedPeople.push({ personId: person.id, name: person.fullName });
                    this.lightboxPersonTagQuery('');
                    this.lightboxPersonTagResults([]);
                } else toast.error('Could not tag person.');
            } catch { toast.error('Error tagging person.'); }
        };

        untagPersonFromLightbox = async (personId) => {
            if (!this.lightboxCanEdit()) return;
            const pid = this.lightboxPhotoId();
            if (!pid) return;
            try {
                const res = await fetch(`/api/photos/${pid}/tag-person/${personId}`, { method: 'DELETE' });
                if (res.ok) {
                    this.lightboxTaggedPeople(this.lightboxTaggedPeople().filter(p => p.personId !== personId));
                } else toast.error('Could not remove tag.');
            } catch { toast.error('Error removing tag.'); }
        };

        initLightboxAnnotorious = async () => {
            if (this._lightboxAnno) {
                try { this._lightboxAnno.destroy(); } catch { /* ignore */ }
                this._lightboxAnno = null;
            }

            if (typeof Annotorious === 'undefined') return;
            if (!this.lightboxCanEdit() && this.lightboxHasAnnotations() && !this.lightboxShowAnnotations()) return;

            const img = document.getElementById('lightboxAnnotatableImage');
            if (!img || !img.src) return;

            if (!img.complete) {
                await new Promise(resolve => img.addEventListener('load', resolve, { once: true }));
            }

            const anno = Annotorious.createImageAnnotator(img);
            this._lightboxAnno = anno;
            this.lightboxSelectedAnnotationId(null);
            this._ensureAnnotationHoverLabel();

            anno.on?.('selectionChanged', (selection) => {
                const selected = Array.isArray(selection) && selection.length > 0 ? selection[0] : null;
                this.lightboxSelectedAnnotationId(selected?.id || null);
            });

            anno.on?.('mouseEnterAnnotation', (annotation) => {
                const label = this._getAnnotationComment(annotation);
                if (!label) return;
                this._showAnnotationHoverLabel(label);
            });

            anno.on?.('mouseLeaveAnnotation', () => {
                this._hideAnnotationHoverLabel();
            });

            if (this.lightboxCanEdit()) {
                anno.on?.('createAnnotation', (annotation) => {
                    const existing = this._getAnnotationComment(annotation);
                    if (existing) return;
                    const text = prompt('Annotation text:', '');
                    if (text && text.trim()) {
                        const updated = this._setAnnotationComment(annotation, text.trim());
                        this._applyAnnotationUpdate(anno, annotation, updated);
                    }
                });
            } else {
                anno.setDrawingEnabled?.(false);
            }

            const data = this._lightboxPhotoData;
            if (data?.annotationsJson) {
                try {
                    const annotations = JSON.parse(data.annotationsJson);
                    await anno.setAnnotations(annotations);
                } catch (e) { console.warn('Failed to parse annotations', e); }
            }
        };

        setAnnotationTool = (tool) => {
            if (!this._lightboxAnno || !this.lightboxCanEdit()) return;
            if (tool === 'rectangle') {
                this._lightboxAnno.setDrawingTool?.('rectangle');
                this._lightboxAnno.setDrawingEnabled?.(true);
                return;
            }
            if (tool === 'polygon') {
                this._lightboxAnno.setDrawingTool?.('polygon');
                this._lightboxAnno.setDrawingEnabled?.(true);
            }
        };

        editSelectedAnnotationText = async () => {
            if (!this._lightboxAnno || !this.lightboxCanEdit()) return;
            const selected = await this._getSelectedAnnotation();
            if (!selected) { toast.info('Select an annotation first.'); return; }
            const current = this._getAnnotationComment(selected) || '';
            const text = prompt('Annotation text:', current);
            if (text == null) return;
            const updated = this._setAnnotationComment(selected, text.trim());
            this._applyAnnotationUpdate(this._lightboxAnno, selected, updated);
        };

        deleteSelectedAnnotation = async () => {
            if (!this._lightboxAnno || !this.lightboxCanEdit()) return;
            const selected = await this._getSelectedAnnotation();
            if (!selected) { toast.info('Select an annotation first.'); return; }
            if (!confirm('Delete selected annotation?')) return;
            this._lightboxAnno.removeAnnotation?.(selected.id);
            this.lightboxSelectedAnnotationId(null);
        };

        _getSelectedAnnotation = async () => {
            if (!this._lightboxAnno) return null;
            const id = this.lightboxSelectedAnnotationId();
            if (!id) return null;
            const all = await this._lightboxAnno.getAnnotations();
            return all.find(a => a.id === id) || null;
        };

        _getAnnotationComment = (annotation) => {
            const rawBodies = annotation?.bodies ?? annotation?.body;
            const bodies = Array.isArray(rawBodies) ? rawBodies : (rawBodies ? [rawBodies] : []);
            const body = bodies.find(b => (b.purpose === 'commenting' || b.purpose === 'describing') && typeof b.value === 'string');
            return body?.value || '';
        };

        _setAnnotationComment = (annotation, text) => {
            const clone = structuredClone(annotation);
            const rawBodies = clone.bodies ?? clone.body;
            const bodies = Array.isArray(rawBodies) ? rawBodies : (rawBodies ? [rawBodies] : []);
            const filtered = bodies.filter(b => !(b.purpose === 'commenting' || b.purpose === 'describing'));
            if (text) {
                filtered.push({ type: 'TextualBody', purpose: 'commenting', value: text });
            }
            clone.bodies = filtered;
            if (clone.body !== undefined) delete clone.body;
            return clone;
        };

        _applyAnnotationUpdate = (anno, oldAnnotation, updatedAnnotation) => {
            if (!anno?.updateAnnotation) return;
            try {
                if (anno.updateAnnotation.length >= 2) {
                    anno.updateAnnotation(oldAnnotation, updatedAnnotation);
                } else {
                    anno.updateAnnotation(updatedAnnotation);
                }
            } catch {
                // Last-resort fallback: replace annotation by remove+add
                try {
                    anno.removeAnnotation?.(oldAnnotation.id);
                    anno.addAnnotation?.(updatedAnnotation);
                } catch { /* ignore */ }
            }
        };

        _ensureAnnotationHoverLabel = () => {
            if (this._annotationHoverLabelEl) return;

            const area = document.getElementById('lightboxImageArea');
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

        saveLightboxAnnotations = async () => {
            const pid = this.lightboxPhotoId();
            if (!pid || !this._lightboxAnno || !this.lightboxCanEdit()) return;

            try {
                const annotations = await this._lightboxAnno.getAnnotations();
                const body = { annotationsJson: JSON.stringify(annotations) };
                const res = await fetch(`/api/photos/${pid}/annotations`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) toast.success('Annotations saved!');
                else toast.error('Failed to save annotations.');
            } catch { toast.error('Error saving annotations.'); }
        };

        startEditPhoto = (photo) => {
            if (!this.canEditMedia(photo)) {
                toast.info('Only the uploader (or an admin) can edit this photo.');
                return;
            }
            this.editingPhotoId(photo.id);
            this.editPhotoTitle(photo.title);
            this.editPhotoDescription(photo.description || '');
            this.editPhotoYear(photo.yearTaken);
            this.editPhotoMonth(photo.monthTaken);
            this.editPhotoDay(photo.dayTaken);
            this.editPhotoTagsText((photo.tags || []).join(', '));
            this.editPhotoSelectedEventIds(photo.eventIds || []);
            this.editPhotoFolderId(photo.folderId ?? null);
            this.editPhotoLatitude(photo.latitude ?? null);
            this.editPhotoLongitude(photo.longitude ?? null);
            this.editPhotoLocationLockedByExif(photo.latitude != null && photo.longitude != null);
            this.editPhotoLocationSearchQuery('');
            this.editPhotoLocationSearchResults([]);
            if (this._editPhotoLocationMap) {
                try { this._editPhotoLocationMap.remove(); } catch { /* ignore */ }
                this._editPhotoLocationMap = null;
                this._editPhotoLocationMarker = null;
            }
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
                eventIds: this.editPhotoSelectedEventIds(),
                folderId: this.editPhotoFolderId() || null,
                latitude: this.editPhotoLatitude() != null ? Number(this.editPhotoLatitude()) : null,
                longitude: this.editPhotoLongitude() != null ? Number(this.editPhotoLongitude()) : null
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
            this.photoFolderId(this.currentMediaFolderId());
            this.initTagify('photoTags');
        };
        cancelPhotoUpload = () => this.isUploadingPhoto(false);

        showVideoUpload = () => {
            this.isUploadingVideo(true);
            this.videoSelectedEventIds([]);
            this.videoFolderId(this.currentMediaFolderId());
            this.initTagify('videoTags');
        };
        cancelVideoUpload = () => this.isUploadingVideo(false);

        showDocumentUpload = () => {
            this.isUploadingDocument(true);
            this.initTagify('documentTags');
        };
        cancelDocumentUpload = () => this.isUploadingDocument(false);

        showAddFolder = () => {
            this.isAddingFolder(true);
            this.newFolderName('');
            this.newFolderDescription('');
        };

        cancelAddFolder = () => {
            this.isAddingFolder(false);
            this.newFolderName('');
            this.newFolderDescription('');
        };

        createMediaFolder = async () => {
            const name = (this.newFolderName() || '').trim();
            if (!name) { toast.error('Folder name is required.'); return; }

            try {
                const res = await fetch('/api/media-folders', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        name,
                        description: this.newFolderDescription() || null
                    })
                });
                if (res.ok) {
                    const data = await res.json();
                    const folder = data.value || data;
                    this.mediaFolders([...this.mediaFolders(), folder].sort((a, b) => (a.name || '').localeCompare(b.name || '')));
                    this.cancelAddFolder();
                    toast.success('Folder created.');
                } else {
                    toast.error('Failed to create folder.');
                }
            } catch { toast.error('Error creating folder.'); }
        };

        openMediaFolder = (folder) => {
            this.currentMediaFolderId(folder.id);
            this.isAddingFolder(false);
            this.isUploadingPhoto(false);
            this.isUploadingVideo(false);
        };

        openTaggedMediaFolder = () => {
            this.currentMediaFolderId(TAGGED_FOLDER_KEY);
            this.isAddingFolder(false);
            this.isUploadingPhoto(false);
            this.isUploadingVideo(false);
        };

        goBackFromFolder = () => {
            this.currentMediaFolderId(null);
        };

        folderPhotoCount = (folderId) => this.ownedPhotos().filter(p => p.folderId === folderId).length;
        folderVideoCount = (folderId) => this.ownedVideos().filter(v => v.folderId === folderId).length;
        taggedPhotoCount = () => this.taggedPhotos().length;
        taggedVideoCount = () => this.taggedVideos().length;

        isProfileOwnedMedia = (item) => {
            if (!item) return false;
            return item.uploadedByPersonId === this.personId();
        };

        canEditMedia = (item) => {
            if (!item) return false;
            if (this.isAdminUser) return true;
            const meId = this.currentUserPersonId();
            return meId != null && item.uploadedByPersonId === meId;
        };

        canManageFolder = (folder) => {
            if (!folder) return false;
            if (this.isAdminUser) return true;
            const meId = this.currentUserPersonId();
            return meId != null && folder.createdByPersonId === meId;
        };

        deleteMediaFolder = async (folder, _vm, event) => {
            event?.preventDefault?.();
            event?.stopPropagation?.();
            if (!folder?.id || !this.canManageFolder(folder)) return;
            if (!confirm(`Delete folder "${folder.name || 'Folder'}"? Media will become ungrouped.`)) return;

            try {
                const res = await fetch(`/api/media-folders/${folder.id}`, { method: 'DELETE' });
                if (!res.ok) {
                    toast.error('Failed to delete folder.');
                    return;
                }

                if (this.currentMediaFolderId() === folder.id) this.currentMediaFolderId(null);
                await this.loadContent();
                toast.success('Folder deleted.');
            } catch {
                toast.error('Error deleting folder.');
            }
        };

        deletePhoto = async (photo) => {
            if (!photo?.id || !this.canEditMedia(photo)) return;
            if (!confirm(`Delete photo "${photo.title || 'Untitled'}"?`)) return;

            try {
                const res = await fetch(`/api/photos/${photo.id}`, { method: 'DELETE' });
                if (!res.ok) {
                    toast.error('Failed to delete photo.');
                    return;
                }

                await this.loadContent();
                toast.success('Photo deleted.');
            } catch {
                toast.error('Error deleting photo.');
            }
        };

        deleteVideo = async (video) => {
            if (!video?.id || !this.canEditMedia(video)) return;
            if (!confirm(`Delete video "${video.title || 'Untitled'}"?`)) return;

            try {
                const res = await fetch(`/api/videos/${video.id}`, { method: 'DELETE' });
                if (!res.ok) {
                    toast.error('Failed to delete video.');
                    return;
                }

                await this.loadContent();
                toast.success('Video deleted.');
            } catch {
                toast.error('Error deleting video.');
            }
        };

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
            if (this.photoFolderId() != null && this.photoFolderId() !== '') formData.append('folderId', String(this.photoFolderId()));

            const response = await fetch('/api/photos', { method: 'POST', body: formData });
            if (response.ok) {
                this.isUploadingPhoto(false);
                this.photoTitle('');
                this.photoDescription('');
                this.photoYearTaken(null);
                this.photoMonthTaken(null);
                this.photoDayTaken(null);
                this.photoSelectedEventIds([]);
                this.photoFolderId(null);
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
            if (this.videoFolderId() != null && this.videoFolderId() !== '') formData.append('folderId', String(this.videoFolderId()));

            const response = await fetch('/api/videos', { method: 'POST', body: formData });
            if (response.ok) {
                this.isUploadingVideo(false);
                this.videoTitle('');
                this.videoDescription('');
                this.videoSelectedEventIds([]);
                this.videoFolderId(null);
                await this.loadContent();
                toast.success('Video uploaded!');
            } else toast.error('Failed to upload video.');
        };

        startEditVideo = (video) => {
            if (!this.canEditMedia(video)) {
                toast.info('Only the uploader (or an admin) can edit this video.');
                return;
            }
            this.editingVideoId(video.id);
            this.editVideoTitle(video.title || '');
            this.editVideoDescription(video.description || '');
            this.editVideoTagsText((video.tags || []).join(', '));
            this.editVideoFolderId(video.folderId ?? null);
            const el = document.getElementById('editVideoModal');
            if (el) bootstrap.Modal.getOrCreateInstance(el).show();
        };

        saveEditVideo = async () => {
            const id = this.editingVideoId();
            if (!id) return;

            const tags = this.editVideoTagsText().split(',').map(t => t.trim()).filter(Boolean);
            const body = {
                title: this.editVideoTitle(),
                description: this.editVideoDescription() || null,
                tags,
                folderId: this.editVideoFolderId() || null
            };

            try {
                const res = await fetch(`/api/videos/${id}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) {
                    bootstrap.Modal.getInstance(document.getElementById('editVideoModal'))?.hide();
                    this.editingVideoId(null);
                    await this.loadContent();
                    toast.success('Video updated!');
                } else toast.error('Failed to update video.');
            } catch { toast.error('Error updating video.'); }
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

        const editEventModal = document.getElementById('editEventModal');
        if (editEventModal) {
            editEventModal.addEventListener('shown.bs.modal', () => vm.initEditEventMap());
        }

        const editPhotoModal = document.getElementById('editPhotoModal');
        if (editPhotoModal) {
            editPhotoModal.addEventListener('shown.bs.modal', () => vm.initEditPhotoLocationMap());
        }

        const lightboxModal = document.getElementById('photoLightboxModal');
        if (lightboxModal) {
            lightboxModal.addEventListener('shown.bs.modal', () => vm.initLightboxAnnotorious());
            lightboxModal.addEventListener('hidden.bs.modal', () => {
                if (vm._lightboxAnno) {
                    try { vm._lightboxAnno.destroy(); } catch { /* ignore */ }
                    vm._lightboxAnno = null;
                }
            });
        }

        await vm.loadProfile();
    });
})();
