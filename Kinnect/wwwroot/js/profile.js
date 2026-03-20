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

        this.isUploadingPhoto = ko.observable(false);
        this.photoTitle = ko.observable('');
        this.photoDescription = ko.observable('');

        this.isUploadingVideo = ko.observable(false);
        this.videoTitle = ko.observable('');
        this.videoDescription = ko.observable('');

        this.isUploadingDocument = ko.observable(false);
        this.documentTitle = ko.observable('');
        this.documentDescription = ko.observable('');

        this._tagifyInstances = {};
        this._personSnapshot = null;
    }

    loadProfile = async () => {
        try {
            const response = await fetch('/api/people/me');
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

        const [postsRes, photosRes, videosRes, docsRes] = await Promise.all([
            fetch(`/api/posts/person/${pid}`),
            fetch(`/api/photos/person/${pid}`),
            fetch(`/api/videos/person/${pid}`),
            fetch(`/api/documents/person/${pid}`)
        ]);

        const postsData = await postsRes.json();
        const photosData = await photosRes.json();
        const videosData = await videosRes.json();
        const docsData = await docsRes.json();

        this.posts(postsData.value || postsData || []);
        this.photos(photosData.value || photosData || []);
        this.videos(videosData.value || videosData || []);
        this.documents(docsData.value || docsData || []);
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
            motherId: snapshot.motherId ?? null
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

    showPhotoUpload = () => {
        this.isUploadingPhoto(true);
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
            const whitelist = (tagsResult.value || tagsResult || []).map(t => t.name);

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

        const response = await fetch('/api/photos', { method: 'POST', body: formData });
        if (response.ok) {
            this.isUploadingPhoto(false);
            this.photoTitle('');
            this.photoDescription('');
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
