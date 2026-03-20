class ViewProfileViewModel {
    constructor(personId) {
        this.loading = ko.observable(true);
        this.personId = personId;
        this.fullName = ko.observable('');
        this.profileImagePath = ko.observable(null);
        this.bio = ko.observable(null);
        this.birthInfo = ko.observable('');
        this.canEdit = ko.observable(false);

        this.posts = ko.observableArray([]);
        this.photos = ko.observableArray([]);
        this.videos = ko.observableArray([]);
        this.documents = ko.observableArray([]);
    }

    loadProfile = async () => {
        try {
            const personRes = await fetch(`/api/people/${this.personId}`);
            const personResult = await personRes.json();
            const person = personResult.value || personResult;

            this.fullName(`${person.givenNames} ${person.familyName}`);
            this.profileImagePath(person.profileImagePath);
            this.bio(person.bio);

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

            const meRes = await fetch('/api/people/me');
            if (meRes.ok) {
                const meResult = await meRes.json();
                const me = meResult.value || meResult;
                this.canEdit(me.id === this.personId || !person.hasAccount);
            }

            const [postsRes, photosRes, videosRes, docsRes] = await Promise.all([
                fetch(`/api/posts/person/${this.personId}`),
                fetch(`/api/photos/person/${this.personId}`),
                fetch(`/api/videos/person/${this.personId}`),
                fetch(`/api/documents/person/${this.personId}`)
            ]);

            const postsData = await postsRes.json();
            const photosData = await photosRes.json();
            const videosData = await videosRes.json();
            const docsData = await docsRes.json();

            this.posts(postsData.value || postsData || []);
            this.photos(photosData.value || photosData || []);
            this.videos(videosData.value || videosData || []);
            this.documents(docsData.value || docsData || []);
        } catch (err) {
            console.error('Error loading profile:', err);
        } finally {
            this.loading(false);
        }
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    const vm = new ViewProfileViewModel(viewPersonId);
    ko.applyBindings(vm);
    await vm.loadProfile();
});
