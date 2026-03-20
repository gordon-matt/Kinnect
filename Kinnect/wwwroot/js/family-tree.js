document.addEventListener('DOMContentLoaded', async () => {
    const response = await fetch('/api/people/family-tree');
    const result = await response.json();
    const data = result.value || result || [];

    if (data.length === 0) {
        document.getElementById('FamilyChart').innerHTML =
            '<div class="d-flex align-items-center justify-content-center h-100 text-muted">' +
            '<div class="text-center"><i class="bi bi-diagram-3" style="font-size:4rem;"></i>' +
            '<p class="mt-2">No family tree data yet. Add people to get started.</p></div></div>';
        return;
    }

    const f3Chart = f3.createChart('#FamilyChart', data)
        .setCardYSpacing(100)
        .setCardXSpacing(40)
        .setOrientationVertical();

    const f3Card = f3Chart.cardHtml()
        .setCardDisplay(cardDisplay)
        .setMiniTree(true)
        .setStyle('card-style')
        .setOnCardClick((e, d) => {
            f3Chart.updateMainId(d.id);
        });

    const f3EditTree = f3Chart.editTree()
        .setFields(['first name', 'last name', 'birthday', 'gender'])
        .setOnSubmit((e, datum, applyChanges, postSubmit) => {
            applyChanges();
            savePersonFromDatum(datum);
            postSubmit();
        })
        .setOnDelete((datum, deletePerson, postSubmit) => {
            if (confirm('Are you sure you want to remove this person?')) {
                deletePerson();
                deletePersonFromServer(datum.data.personId);
                postSubmit();
            }
        })
        .setCardClickOpen(f3Card);

    f3Chart.updateTree({ initial: true });

    function cardDisplay(d) {
        const data = d.data;
        const genderIcon = data.gender === 'M' ? 'bi-gender-male' : 'bi-gender-female';
        const genderColor = data.gender === 'M' ? '#4a90d9' : '#d94a8c';
        const avatarHtml = data.avatar
            ? `<img src="/uploads/${data.avatar}" style="width:50px;height:50px;border-radius:50%;object-fit:cover;margin-right:8px;" />`
            : `<div style="width:50px;height:50px;border-radius:50%;background:${genderColor};color:#fff;display:flex;align-items:center;justify-content:center;margin-right:8px;font-size:1.2rem;"><i class="bi ${genderIcon}"></i></div>`;

        const profileLink = data.personId
            ? `<a href="/Profile/View/${data.personId}" class="card-profile-link" title="View Profile" onclick="event.stopPropagation();"><i class="bi bi-person-badge"></i></a>`
            : '';

        return `
            <div style="display:flex;align-items:center;padding:8px 12px;min-width:180px;cursor:pointer;position:relative;">
                ${avatarHtml}
                <div style="flex:1;">
                    <div style="font-weight:600;font-size:0.9rem;">${data['first name'] || ''} ${data['last name'] || ''}</div>
                    <div style="font-size:0.75rem;color:#888;">${data.birthday || ''}</div>
                </div>
                ${profileLink}
            </div>`;
    }

    async function savePersonFromDatum(datum) {
        const data = datum.data;
        const personId = data.personId;
        const body = {
            givenNames: data['first name'] || '',
            familyName: data['last name'] || '',
            isMale: data.gender === 'M',
        };

        if (data.birthday) {
            const parts = data.birthday.split('-');
            if (parts.length >= 1) body.yearOfBirth = parseInt(parts[0]) || null;
            if (parts.length >= 2) body.monthOfBirth = parseInt(parts[1]) || null;
            if (parts.length >= 3) body.dayOfBirth = parseInt(parts[2]) || null;
        }

        if (datum.rels) {
            if (datum.rels.parents && datum.rels.parents.length > 0) {
                for (const parentId of datum.rels.parents) {
                    const parent = data._store ? data._store.getDatum(parentId) : null;
                    if (parent) {
                        if (parent.data.gender === 'M') body.fatherId = parent.data.personId;
                        else body.motherId = parent.data.personId;
                    }
                }
            }
        }

        try {
            if (personId) {
                await fetch(`/api/people/${personId}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
            } else {
                const response = await fetch('/api/people', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (response.ok) {
                    const result = await response.json();
                    const created = result.value || result;
                    datum.data.personId = created.id;
                }
            }
        } catch (err) {
            console.error('Error saving person:', err);
        }
    }

    async function deletePersonFromServer(personId) {
        if (!personId) return;
        try {
            await fetch(`/api/people/${personId}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting person:', err);
        }
    }
});
