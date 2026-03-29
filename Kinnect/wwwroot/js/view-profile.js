const MONTHS = [
    { v: null, l: '— Month —' },
    { v: 1,  l: 'January' },  { v: 2,  l: 'February' }, { v: 3,  l: 'March' },
    { v: 4,  l: 'April' },    { v: 5,  l: 'May' },       { v: 6,  l: 'June' },
    { v: 7,  l: 'July' },     { v: 8,  l: 'August' },    { v: 9,  l: 'September' },
    { v: 10, l: 'October' },  { v: 11, l: 'November' },  { v: 12, l: 'December' }
];
const TAGGED_FOLDER_KEY = '__tagged__';

function daysInMonth(monthObs, yearObs) {
    return ko.computed(() => {
        const m = parseInt(ko.unwrap(monthObs)) || 0;
        const y = parseInt(ko.unwrap(yearObs)) || 2000;
        const count = m ? new Date(y, m, 0).getDate() : 31;
        return [null, ...Array.from({ length: count }, (_, i) => i + 1)];
    });
}

function partialDateDisplay(year, month, day) {
    if (year == null) return null;
    if (month != null && day != null)
        return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    return String(year);
}

class ViewProfileViewModel {
    constructor(personId, isAdminUser) {
        this.loading = ko.observable(true);
        this.personId = personId;
        this.isAdminUser = isAdminUser;
        this.fullName = ko.observable('');
        this.profileImagePath = ko.observable(null);
        this.bio = ko.observable(null);
        this.note = ko.observable(null);
        this.isMale = ko.observable(null);
        this.latitude = ko.observable(null);
        this.longitude = ko.observable(null);
        this.birthInfo = ko.observable('');
        this.occupation = ko.observable(null);
        this.education = ko.observable(null);
        this.religion = ko.observable(null);
        this.hasAccount = ko.observable(false);
        this.isDeceased = ko.observable(false);
        this.userId = ko.observable(null);
        this.canEdit = ko.observable(false);
        this.canEditOwn = ko.observable(false);

        this.posts = ko.observableArray([]);
        this.photos = ko.observableArray([]);
        this.videos = ko.observableArray([]);
        this._videoProcessingPollTimer = null;
        this.documents = ko.observableArray([]);
        this.mediaFolders = ko.observableArray([]);
        this.currentMediaFolderId = ko.observable(null);
        this.currentUserPersonId = ko.observable(null);
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

        // Merged timeline with spouse events (item 1)
        this.timelineItems = ko.computed(() => {
            const items = [];

            for (const ev of this.events()) {
                items.push({ ...ev, _source: 'event' });
            }

            for (const sp of this.spouses()) {
                const name = sp.displayName;
                if (sp.engagementYear) {
                    const y = sp.engagementYear, mo = sp.engagementMonth, d = sp.engagementDay;
                    items.push({
                        _source: 'spouse', id: null, eventType: 'ENGA', eventTypeLabel: 'Engagement',
                        year: y, month: mo, day: d,
                        dateDisplay: partialDateDisplay(y, mo, d),
                        place: null, description: `To ${name}`
                    });
                }
                if (sp.marriageYear) {
                    const y = sp.marriageYear, mo = sp.marriageMonth, d = sp.marriageDay;
                    items.push({
                        _source: 'spouse', id: null, eventType: 'MARR', eventTypeLabel: 'Marriage',
                        year: y, month: mo, day: d,
                        dateDisplay: partialDateDisplay(y, mo, d),
                        place: null, description: `To ${name}`
                    });
                }
                if (sp.divorceYear) {
                    const y = sp.divorceYear, mo = sp.divorceMonth, d = sp.divorceDay;
                    items.push({
                        _source: 'spouse', id: null, eventType: 'DIV', eventTypeLabel: 'Divorce',
                        year: y, month: mo, day: d,
                        dateDisplay: partialDateDisplay(y, mo, d),
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
        this.hasEventMapItems = ko.computed(() => this.timelineItems().length > 0);
        this.hasPhotoMapItems = ko.computed(() =>
            this.photos().some(p => p?.latitude != null && p?.longitude != null));

        this.videoLightboxUrl = ko.observable(null);
        this.videoLightboxTitle = ko.observable('');

        this.photoLightboxUrl = ko.observable(null);
        this.lightboxPhotoId = ko.observable(null);
        this.lightboxPhotoTitle = ko.observable('');
        this.lightboxPhotoDate = ko.observable('');
        this.lightboxCanEdit = ko.observable(false);
        this.lightboxHasAnnotations = ko.observable(false);
        this.lightboxShowAnnotations = ko.observable(true);
        this.lightboxTaggedPeople = ko.observableArray([]);
        this._lightboxAnno = null;
        this._lightboxPhotoData = null;
        this.lightboxSelectedAnnotationId = ko.observable(null);
        this._annotationHoverLabelEl = null;
        this._lastMouseX = 0;
        this._lastMouseY = 0;
        this.lightboxShowAnnotations.subscribe(() => {
            if (!this.lightboxCanEdit()) this.initLightboxAnnotorious();
        });

        this.MONTHS = MONTHS;

        // Invite modal observables
        this.inviteEmail = ko.observable('');
        this.inviteSubject = ko.observable('');
        this.inviteBody = ko.observable('');
        this.inviteSending = ko.observable(false);
    }

    showNotSignedUpMessage = () => {
        const name = this.fullName() || 'This person';
        toast.info(`${name} is not yet signed up. Invite them to join.`);
    };

    prepareInviteDefaults = () => {
        const name = this.fullName() || 'someone';
        this.inviteEmail('');
        this.inviteSubject(`You're invited to join Kinnect`);
        this.inviteBody(
            `Hi ${name},\n\n` +
            `You've been invited to join our family tree on Kinnect. ` +
            `Your profile has already been created — just sign up and claim it!\n\n` +
            `Visit: ${window.location.origin}\n\n` +
            `See you there!`
        );
    };

    sendInvite = async () => {
        const email = this.inviteEmail().trim();
        if (!email) {
            toast.warning('Please enter an email address.');
            return;
        }

        this.inviteSending(true);
        try {
            const res = await fetch(`/api/people/${this.personId}/invite`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    email,
                    subject: this.inviteSubject(),
                    body: this.inviteBody()
                })
            });

            if (res.ok) {
                bootstrap.Modal.getInstance(document.getElementById('inviteModal'))?.hide();
                toast.success('Invite sent successfully!');
            } else {
                const err = await res.json().catch(() => ({}));
                toast.error('Failed to send invite: ' + (err?.title || 'Unknown error'));
            }
        } catch {
            toast.error('An error occurred while sending the invite.');
        } finally {
            this.inviteSending(false);
        }
    };

    loadProfile = async () => {
        try {
            const personRes = await fetch(`/api/people/${this.personId}`);
            const personResult = await personRes.json();
            const person = personResult.value || personResult;

            this.fullName(`${person.givenNames} ${person.familyName}`);
            this.profileImagePath(person.profileImagePath);
            this.bio(person.bio);
            this.note(person.note);
            this.isMale(person.isMale ?? null);
            this.latitude(person.latitude ?? null);
            this.longitude(person.longitude ?? null);
            this.occupation(person.occupation);
            this.education(person.education);
            this.religion(person.religion);
            this.hasAccount(!!person.userId);
            this.isDeceased(!!person.isDeceased);
            this.userId(person.userId ?? null);

            this.birthInfo('');

            // Determine edit permissions based on current user and person association.
            // Admins can always edit; anyone can edit a person with no linked user account;
            // users can edit their own linked person.
            try {
                const meRes = await fetch('/api/people/me');
                if (meRes.ok) {
                    const meData = await meRes.json();
                    const me = meData.value || meData;
                    this.currentUserPersonId(me?.id ?? null);
                    const isOwn = me?.id != null && me.id === this.personId;
                    this.canEditOwn(isOwn);
                    this.canEdit(this.isAdminUser || !person.userId || isOwn);
                } else {
                    this.currentUserPersonId(null);
                    this.canEditOwn(false);
                    this.canEdit(this.isAdminUser || !person.userId);
                }
            } catch {
                this.currentUserPersonId(null);
                this.canEditOwn(false);
                this.canEdit(this.isAdminUser || !person.userId);
            }

            const [postsRes, photosRes, videosRes, docsRes, eventsRes, spousesRes, foldersRes] = await Promise.all([
                fetch(`/api/posts/person/${this.personId}`),
                fetch(`/api/photos/person/${this.personId}`),
                fetch(`/api/videos/person/${this.personId}`),
                fetch(`/api/documents/person/${this.personId}`),
                fetch(`/api/people/${this.personId}/events`),
                fetch(`/api/people/${this.personId}/spouses`),
                fetch(`/api/media-folders/person/${this.personId}`)
            ]);

            const postsData = await postsRes.json();
            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();
            const eventsData = await eventsRes.json();
            const spousesData = await spousesRes.json();
            const foldersData = await foldersRes.json();

            this.posts(postsData.value || postsData || []);
            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
            this.events(eventsData.value || eventsData || []);
            this.mediaFolders(foldersData.value || foldersData || []);
            this.scheduleVideoProcessingPollIfNeeded();

            const spouseList = spousesData.value || spousesData || [];
            this.spouses(spouseList.map(s => ({
                spousePersonId: s.spousePersonId,
                displayName: `${s.givenNames} ${s.familyName}`.trim(),
                hasEngagement: s.hasEngagement ?? false,
                hasMarriage: s.hasMarriage ?? false,
                hasDivorce: s.hasDivorce ?? false,
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

            // Initialise readonly location map if coordinates are available
            const lat = this.latitude();
            const lng = this.longitude();
            if (lat != null && lng != null) {
                setTimeout(() => {
                    const mapEl = document.getElementById('viewProfileMap');
                    if (!mapEl || mapEl._leafletInitialised) return;
                    mapEl._leafletInitialised = true;
                    const map = L.map(mapEl, { zoomControl: true, scrollWheelZoom: false }).setView([lat, lng], 13);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                        attribution: '© OpenStreetMap contributors'
                    }).addTo(map);
                    L.marker([lat, lng]).addTo(map);
                }, 50);
            }
        } catch (err) {
            console.error('Error loading profile:', err);
        } finally {
            this.loading(false);
        }
    };

    scheduleVideoProcessingPollIfNeeded = () => {
        clearTimeout(this._videoProcessingPollTimer);
        if (!this.videos().some((v) => v.isProcessing)) return;
        this._videoProcessingPollTimer = setTimeout(async () => {
            await this.loadProfile();
        }, 8000);
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

    openVideoLightbox = (video) => {
        if (!video || video.isProcessing) return;
        this.videoLightboxUrl('/uploads/' + video.filePath);
        this.videoLightboxTitle(video.title || '');
        const el = document.getElementById('videoLightboxModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();
    };

    openPhotoLightbox = async (photo) => {
        this.photoLightboxUrl('/uploads/' + photo.filePath);
        this.lightboxPhotoId(photo.id);
        this.lightboxPhotoTitle(photo.title || '');
        this.lightboxPhotoDate(this.formatPhotoDate(photo));
        // Read-only page: never allow editing/tweaking photo metadata or annotations.
        this.lightboxCanEdit(false);
        this.lightboxHasAnnotations(!!photo.annotationsJson);
        this.lightboxShowAnnotations(true);
        this.lightboxTaggedPeople(photo.taggedPeople || []);

        const el = document.getElementById('photoLightboxModal');
        if (el) bootstrap.Modal.getOrCreateInstance(el).show();

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

    openMediaFolder = (folder) => {
        this.currentMediaFolderId(folder.id);
    };

    openTaggedMediaFolder = () => {
        this.currentMediaFolderId(TAGGED_FOLDER_KEY);
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
        // Ownership grouping is strictly by uploader == viewed profile person.
        return item.uploadedByPersonId === this.personId;
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

        const data = this._lightboxPhotoData;
        if (data?.annotationsJson) {
            try {
                const annotations = JSON.parse(data.annotationsJson);
                await anno.setAnnotations(annotations);
            } catch (e) { console.warn('Failed to parse annotations', e); }
        }

        // Read-only: never allow drawing/editing annotations.
        anno.setDrawingEnabled(false);
    };

    // Read-only photo lightbox: no annotation editing helpers.

    _getAnnotationComment = (annotation) => {
        const rawBodies = annotation?.bodies ?? annotation?.body;
        const bodies = Array.isArray(rawBodies) ? rawBodies : (rawBodies ? [rawBodies] : []);
        const body = bodies.find(b => (b.purpose === 'commenting' || b.purpose === 'describing') && typeof b.value === 'string');
        return body?.value || '';
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
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new ViewProfileViewModel(viewPersonId, isAdmin);
    ko.applyBindings(vm);

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

    const inviteModal = document.getElementById('inviteModal');
    if (inviteModal) {
        inviteModal.addEventListener('show.bs.modal', () => vm.prepareInviteDefaults());
    }

    const videoLightboxModal = document.getElementById('videoLightboxModal');
    if (videoLightboxModal) {
        videoLightboxModal.addEventListener('hidden.bs.modal', () => {
            const v = document.getElementById('videoLightboxPlayer');
            if (v) {
                v.pause();
                v.removeAttribute('src');
                v.load();
            }
            vm.videoLightboxUrl(null);
            vm.videoLightboxTitle('');
        });
    }

    await vm.loadProfile();
});
