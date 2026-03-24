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
        this._lightboxPhotoData = null;
        this._lightboxPersonTagTimer = null;
        this._allPeople = [];
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
        this.editPhotoFolderId = ko.observable(null);

        this.editingVideoId = ko.observable(null);
        this.editVideoTitle = ko.observable('');
        this.editVideoDescription = ko.observable('');
        this.editVideoTagsText = ko.observable('');
        this.editVideoFolderId = ko.observable(null);

        this.MONTHS = MONTHS;
    }

    loadProfile = async () => {
        try {
            // Load all people for person-tagging autocomplete
            fetch('/api/people').then(async r => {
                if (r.ok) {
                    const d = await r.json();
                    this._allPeople = (d.value || d || []).map(p => ({
                        id: p.id,
                        fullName: p.fullName || `${p.givenNames} ${p.familyName}`
                    }));
                }
            });

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
                this.currentUserPersonId(me.id ?? null);
                const isOwnProfile = me.id === this.personId;
                const isUnlinked = !person.userId;
                this.canEditOwn(isOwnProfile);
                this.canEdit(isOwnProfile || isUnlinked || this.isAdminUser);
            } else if (this.isAdminUser) {
                this.canEdit(true);
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
        const results = this._allPeople
            .filter(p => !taggedIds.has(p.id) && p.fullName.toLowerCase().includes(q))
            .slice(0, 6);
        this.lightboxPersonTagResults(results);
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
            await this.loadProfile();
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

            await this.loadProfile();
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

            await this.loadProfile();
            toast.success('Video deleted.');
        } catch {
            toast.error('Error deleting video.');
        }
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
        }

        const data = this._lightboxPhotoData;
        if (data?.annotationsJson) {
            try {
                const annotations = JSON.parse(data.annotationsJson);
                await anno.setAnnotations(annotations);
            } catch (e) { console.warn('Failed to parse annotations', e); }
        }

        if (!this.canEdit()) {
            // Disable drawing in read-only mode — only show existing annotations
            anno.setDrawingEnabled(false);
        }
        if (!this.lightboxCanEdit()) {
            anno.setDrawingEnabled(false);
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
        if (text) filtered.push({ type: 'TextualBody', purpose: 'commenting', value: text });
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
        this.editPhotoFolderId(photo.folderId ?? null);
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
            tags,
            folderId: this.editPhotoFolderId() || null
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
                await this.loadProfile();
                toast.success('Video updated!');
            } else toast.error('Failed to update video.');
        } catch { toast.error('Error updating video.'); }
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
