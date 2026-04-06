document.addEventListener('DOMContentLoaded', async () => {
    const response = await fetch('/api/people/family-tree');
    const result = await response.json();
    const data = result.value || result || [];

    if (data.length === 0) {
        document.getElementById('FamilyChart').innerHTML =
            '<div class="d-flex align-items-center justify-content-center h-100 text-muted">' +
            '<div class="text-center">' +
            '<i class="bi bi-diagram-3" style="font-size:4rem;"></i>' +
            '<p class="mt-2">No family tree data yet.' +
            (isAdmin ? ' Add people to get started.' : '') +
            '</p></div></div>';
        return;
    }

    // Determine the initial person: prefer the logged-in user's person, else first record.
    // Chart defaults main_id to data[0].id; updateTree({ id }) is NOT supported — must call updateMainId().
    let initialId = data[0].id;
    if (myPersonId != null && myPersonId !== '') {
        const myNode = data.find(d => {
            const pid = d.data?.personId;
            return pid != null && Number(pid) === Number(myPersonId);
        });
        if (myNode) initialId = myNode.id;
    }

    const f3Chart = f3.createChart('#FamilyChart', data)
        .setCardYSpacing(150)
        .setCardXSpacing(280);

    const f3Card = f3Chart.setCardHtml()
        .setCardDim({ w: 220, h: 70, height_auto: true })
        .setCardInnerHtmlCreator(cardInnerHtmlCreator)
        .setMiniTree(true);

    if (isAdmin) {
        const f3EditTree = f3Chart.editTree()
            .setFields([
                { id: 'first name',  label: 'First name',  type: 'text' },
                { id: 'last name',   label: 'Last name',   type: 'text' },
                // The library only renders 'text'/'textarea'/'select'/'rel_reference'.
                // We declare birthday as 'text' and upgrade it to type="date" via
                // setOnFormCreation so the browser shows a native date picker.
                { id: 'birthday',    label: 'Birthday',    type: 'text' }
            ])
            .setOnFormCreation(({ cont, form_creator }) => {
                const input = cont.querySelector('input[name="birthday"]');
                if (input) {
                    input.type = 'date';
                    input.classList.add('form-control');
                }

                // Add "Edit Profile" button for persons already saved to the database.
                const datum = f3Chart.store.getDatum(form_creator.datum_id);
                const personId = datum?.data?.personId;
                if (personId) {
                    const form = cont.querySelector('form');
                    if (form && !form.querySelector('.btn-edit-profile')) {
                        const editLink = document.createElement('a');
                        editLink.href = `/Profile/Edit/${personId}`;
                        editLink.className = 'btn btn-sm btn-outline-secondary w-100 btn-edit-profile';
                        editLink.textContent = 'Edit Profile';
                        editLink.style.cssText = 'display:block;margin-top:0.5rem;';
                        form.appendChild(editLink);
                    }
                }
            })
            .setOnSubmit((e, datum, applyChanges, postSubmit) => {
                e.preventDefault();
                const isNewPerson = !datum.data.personId;
                applyChanges();
                savePersonFromDatum(datum).then(() => {
                    postSubmit();
                    if (isNewPerson) {
                        setTimeout(() => {
                            // onCancel() resets the "add relative" state so the Add Son/Daughter
                            // cards disappear. Its internal cancelCallback synchronously reopens
                            // the parent's edit form as a side effect.
                            if (f3EditTree.addRelativeInstance.is_active) {
                                f3EditTree.addRelativeInstance.onCancel();
                            }
                            // closeForm() then hides that form and triggers a clean tree redraw.
                            f3EditTree.closeForm();
                        }, 150);
                    }
                });
            })
            .setOnDelete((datum, deletePerson, postSubmit) => {
                if (confirm('Are you sure you want to remove this person from the tree?')) {
                    deletePerson();
                    if (datum.data.personId) {
                        deletePersonFromServer(datum.data.personId);
                    }
                    postSubmit({});
                }
            })
            .setCardClickOpen(f3Card);

        // The library has no option to disable history navigation, but its own
        // controls object exposes a destroy() method that removes the buttons cleanly.
        f3EditTree.history.controls.destroy();
    } else {
        // Non-editors: show the edit pane in readonly mode so they can view details
        // and navigate to a person's profile, but cannot make changes.
        const f3ReadTree = f3Chart.editTree()
            .setFields([
                { id: 'first name', label: 'First name', type: 'text' },
                { id: 'last name',  label: 'Last name',  type: 'text' },
                { id: 'birthday',   label: 'Birthday',   type: 'text' }
            ])
            .setNoEdit()
            .setCanDelete(() => false)
            .setOnFormCreation(({ cont, form_creator }) => {
                const form = cont.querySelector('form');
                if (!form) return;

                // Upgrade birthday to date type for display.
                const birthdayInput = form.querySelector('input[name="birthday"]');
                if (birthdayInput) birthdayInput.type = 'date';

                // Disable all form controls so data is clearly readonly.
                form.querySelectorAll('input, select, textarea').forEach(el => {
                    el.disabled = true;
                });

                // Relabel "Cancel" → "Close" and hide the submit/delete actions.
                const cancelBtn = form.querySelector('.f3-cancel-btn');
                if (cancelBtn) cancelBtn.textContent = 'Close';

                form.querySelector('button[type="submit"]')?.setAttribute('style', 'display:none');
                form.querySelector('.f3-delete-btn')?.closest('div')?.setAttribute('style', 'display:none');
                form.querySelector('.f3-remove-relative-btn')?.closest('div')?.setAttribute('style', 'display:none');
                const hr = form.querySelector('hr');
                if (hr) hr.style.display = 'none';

                // Add "View Profile" link for persons linked to a profile page.
                const datum = f3Chart.store.getDatum(form_creator.datum_id);
                const personId = datum?.data?.personId;
                if (personId && !form.querySelector('.btn-view-profile')) {
                    const viewLink = document.createElement('a');
                    viewLink.href = `/Profile/View/${personId}`;
                    viewLink.className = 'btn btn-sm btn-outline-primary w-100 btn-view-profile';
                    viewLink.textContent = 'View Profile';
                    viewLink.style.cssText = 'display:block;margin-top:0.5rem;';
                    const formButtons = form.querySelector('.f3-form-buttons');
                    if (formButtons) formButtons.after(viewLink);
                    else form.appendChild(viewLink);
                }
            })
            .setOnSubmit((e) => {
                // Readonly: swallow the submit event so nothing is saved.
                e.preventDefault();
            })
            .setCardClickOpen(f3Card);

        f3ReadTree.history.controls.destroy();
    }

    f3Chart.updateMainId(initialId);
    f3Chart.updateTree({ initial: true });

    // ── Alphabetical people sidebar ────────────────────────────────────────────
    buildSidebar(data, initialId);

    function buildSidebar(treeData, activeChartId) {
        const list = document.getElementById('SidebarPersonList');
        if (!list) return;

        const people = treeData
            .filter(d => d.data)
            .map(d => ({
                chartId: d.id,
                name: `${d.data['first name'] || ''} ${d.data['last name'] || ''}`.trim() || 'Unknown'
            }))
            .sort((a, b) => a.name.localeCompare(b.name));

        list.innerHTML = '';

        people.forEach(person => {
            const item = document.createElement('div');
            item.className = 'sidebar-person-item';
            item.textContent = person.name;
            item.title = person.name;
            item.dataset.chartId = person.chartId;

            if (person.chartId === activeChartId) {
                item.classList.add('active');
            }

            item.addEventListener('click', () => {
                list.querySelectorAll('.sidebar-person-item').forEach(el => el.classList.remove('active'));
                item.classList.add('active');
                f3Chart.updateMainId(person.chartId);
                f3Chart.updateTree({});
            });

            list.appendChild(item);
        });
    }

    // ── Card inner HTML ────────────────────────────────────────────────────────
    function cardInnerHtmlCreator(d) {
        const data = d.data.data;
        const isMale = data.gender === 'M';
        const borderColor = isMale ? '#4a90d9' : '#c0436e';
        const genderIcon = isMale ? '♂' : '♀';
        const name = `${data['first name'] || ''} ${data['last name'] || ''}`.trim() || 'Unknown';
        const birthday = data.birthday || '';
        const personId = data.personId;
        const isCurrentUser = myPersonId && personId === myPersonId;

        const avatarHtml = data.avatar
            ? `<img src="/uploads/${data.avatar}"
                    style="width:46px;height:46px;border-radius:50%;object-fit:cover;flex-shrink:0;" />`
            : `<div style="width:46px;height:46px;border-radius:50%;background:${borderColor};
                    color:#fff;display:flex;align-items:center;justify-content:center;
                    font-size:1.3rem;flex-shrink:0;">${genderIcon}</div>`;

        const profileBtn = personId
            ? `<a href="/Profile/View/${personId}"
                  title="View profile"
                  onclick="event.stopPropagation();"
                  style="position:absolute;top:4px;right:4px;color:#999;font-size:0.75rem;
                         text-decoration:none;line-height:1;"
                  >&#128100;</a>`
            : '';

        const highlightBorder = isCurrentUser ? '2px solid #f0a500' : 'none';

        return `
            <div class="card-inner"
                 style="display:flex;align-items:center;gap:10px;
                        padding:8px 12px;min-width:200px;position:relative;
                        border-left:4px solid ${borderColor};background:#fff;
                        border-radius:6px;box-shadow:0 2px 8px rgba(0,0,0,.1);
                        outline:${highlightBorder};outline-offset:2px;">
                ${avatarHtml}
                <div style="flex:1;min-width:0;">
                    <div style="font-weight:600;font-size:.85rem;white-space:nowrap;
                                overflow:hidden;text-overflow:ellipsis;">${name}</div>
                    <div style="font-size:.72rem;color:#888;">${birthday}</div>
                </div>
                ${profileBtn}
            </div>`;
    }

    // ── Server sync helpers (admin only) ───────────────────────────────────────
    async function upsertBirthEventForPerson(personId, birthdayStr) {
        if (!personId) return;
        const trimmed = (birthdayStr || '').trim();
        if (!trimmed) return;
        const parts = trimmed.split('-');
        const year = parseInt(parts[0], 10) || null;
        const month = parts.length >= 2 ? parseInt(parts[1], 10) || null : null;
        const day = parts.length >= 3 ? parseInt(parts[2], 10) || null : null;
        const evRes = await fetch(`/api/people/${personId}/events`);
        const evJson = await evRes.json();
        const events = evJson.value || evJson || [];
        const birt = events.find((e) => e.eventType === 'BIRT');
        const payload = {
            eventType: 'BIRT',
            year,
            month,
            day,
            place: null,
            description: null,
            note: null
        };
        if (birt) {
            await fetch(`/api/people/${personId}/events/${birt.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
        } else {
            await fetch(`/api/people/${personId}/events`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
        }
    }

    async function savePersonFromDatum(datum) {
        const d = datum.data;
        const personId = d.personId;

        const body = {
            givenNames: d['first name'] || '',
            familyName: d['last name'] || '',
            isMale: d.gender === 'M'
        };

        if (datum.rels?.parents?.length > 0) {
            for (const parentChartId of datum.rels.parents) {
                const parent = f3Chart.store.getDatum(parentChartId);
                if (!parent) continue;
                if (parent.data.gender === 'M') body.fatherId = parent.data.personId || null;
                else body.motherId = parent.data.personId || null;
            }
        }

        try {
            if (personId) {
                const res = await fetch(`/api/people/${personId}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) {
                    await upsertBirthEventForPerson(personId, d.birthday);
                } else {
                    console.error(`Update person failed: ${res.status}`);
                }
            } else {
                const res = await fetch('/api/people', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(body)
                });
                if (res.ok) {
                    const json = await res.json();
                    const newPersonId = (json.value || json).id;
                    datum.data.personId = newPersonId;
                    await upsertBirthEventForPerson(newPersonId, d.birthday);

                    if (newPersonId && datum.rels?.spouses?.length > 0) {
                        for (const spouseChartId of datum.rels.spouses) {
                            const spouseDatum = f3Chart.store.getDatum(spouseChartId);
                            if (spouseDatum?.data?.personId) {
                                await linkSpouseToServer(newPersonId, spouseDatum.data.personId);
                            }
                        }
                    }
                } else {
                    console.error(`Create person failed: ${res.status} ${await res.text()}`);
                }
            }

            await syncParentsForPerson(datum);
            if (datum.rels?.children?.length > 0) {
                for (const childChartId of datum.rels.children) {
                    const childDatum = f3Chart.store.getDatum(childChartId);
                    if (childDatum) await syncParentsForPerson(childDatum);
                }
            }
        } catch (err) {
            console.error('Error saving person:', err);
        }
    }

    async function syncParentsForPerson(datum) {
        const personId = datum?.data?.personId;
        if (!personId) return;

        let fatherId = null;
        let motherId = null;
        if (datum.rels?.parents?.length > 0) {
            for (const parentChartId of datum.rels.parents) {
                const parent = f3Chart.store.getDatum(parentChartId);
                if (!parent?.data?.personId) continue;
                if (parent.data.gender === 'M') fatherId = parent.data.personId;
                else if (parent.data.gender === 'F') motherId = parent.data.personId;
            }
        }
        await updateParentsOnServer(personId, fatherId, motherId);
    }

    async function linkSpouseToServer(personId, spouseId) {
        try {
            const res = await fetch(`/api/people/${personId}/spouse/${spouseId}`, { method: 'POST' });
            if (!res.ok) console.error(`Link spouse failed: ${res.status}`);
        } catch (err) {
            console.error('Error linking spouse:', err);
        }
    }

    async function updateParentsOnServer(personId, fatherId, motherId) {
        try {
            const res = await fetch(`/api/people/${personId}/parents`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ fatherId, motherId })
            });
            if (!res.ok) console.error(`Update parents failed: ${res.status}`);
        } catch (err) {
            console.error('Error updating parents:', err);
        }
    }

    async function deletePersonFromServer(personId) {
        try {
            await fetch(`/api/people/${personId}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting person:', err);
        }
    }
});
