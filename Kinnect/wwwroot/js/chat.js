function formatChatTimestamp(ts) {
    const d = new Date(ts);
    const now = new Date();
    const diffDays = Math.floor((now - d) / 86400000);
    const hh = d.getHours().toString().padStart(2, '0');
    const mm = d.getMinutes().toString().padStart(2, '0');
    if (diffDays === 0) return `${hh}:${mm}`;
    if (diffDays === 1) return `Yesterday ${hh}:${mm}`;
    return `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()} ${hh}:${mm}`;
}

function scrollChatToBottom(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.scrollTop = el.scrollHeight;
}

class ChatRoomRow {
    constructor(data) {
        this.id = ko.observable(data.id);
        this.name = ko.observable(data.name);
        this.adminUserId = ko.observable(data.adminUserId);
    }
}

class ChatMessageRow {
    constructor(data, myUserId) {
        this.id = ko.observable(data.id);
        this.content = ko.observable(data.content);
        this.fromUserId = ko.observable(data.fromUserId);
        this.fromUserName = ko.observable(data.fromUserName || '');
        this.fromFullName = ko.observable(data.fromFullName || data.fromUserName || '?');
        this.timestamp = ko.observable(data.timestamp);
        this.timestampDisplay = ko.pureComputed(() => formatChatTimestamp(this.timestamp()));
        this.isMine = ko.observable(data.fromUserId === myUserId);
    }
}

class OnlineUserRow {
    constructor(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
        this.currentRoom = ko.observable(data.currentRoom || '');
    }
}

class PrivateConversationRow {
    constructor(userId, fullName) {
        this.userId = ko.observable(userId);
        this.fullName = ko.observable(fullName || userId);
    }
}

class ChatProfileInfo {
    constructor(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
        this.isAdmin = ko.observable(!!data.isAdmin);
    }
}

class ChatViewModel {
    constructor(hub) {
        this.hub = hub;

        this.isLoading = ko.observable(true);
        this.serverInfoMessage = ko.observable('');
        this.myProfile = ko.observable(null);
        this.myUserId = ko.observable(null);
        this.isGlobalAdmin = ko.pureComputed(() => this.myProfile() ? !!this.myProfile().isAdmin() : false);

        this.chatRooms = ko.observableArray([]);
        this.chatMessages = ko.observableArray([]);
        this.onlineUsers = ko.observableArray([]);
        this.privateConversations = ko.observableArray([]);
        this.privateMessages = ko.observableArray([]);

        this.activeRoomId = ko.observable(null);
        this.activeRoomName = ko.observable(null);
        this.activePrivateUserId = ko.observable(null);
        this.activePrivateUserName = ko.observable(null);

        this.messageText = ko.observable('');
        this.privateMessageText = ko.observable('');

        this.isRoomAdmin = ko.pureComputed(() => {
            const room = this.chatRooms().find(r => r.id() === this.activeRoomId());
            return !!(room && this.myUserId() && room.adminUserId() === this.myUserId());
        });

        this.isAnnouncementsRoom = ko.pureComputed(() => (this.activeRoomName() || '') === 'Announcements');
        this.canPostToActiveRoom = ko.pureComputed(() => !this.isAnnouncementsRoom() || this.isGlobalAdmin());
        this.canManageActiveRoom = ko.pureComputed(() => this.isRoomAdmin() && !this.isAnnouncementsRoom());
    }

    joinRoom = (room) => {
        this.activePrivateUserId(null);
        this.activePrivateUserName(null);
        this.chatMessages([]);
        this.activeRoomId(room.id());
        this.activeRoomName(room.name());

        this.hub.invoke('Join', room.name()).catch(console.error);
        this.loadRoomMessages(room.id());
    };

    loadRoomMessages = async (roomId) => {
        try {
            const res = await fetch(`/api/chat-messages/room/${roomId}`);
            const data = await res.json();
            const myId = this.myUserId();
            this.chatMessages((data || []).map(m => new ChatMessageRow(m, myId)));
            setTimeout(() => scrollChatToBottom('chat-messages-room'), 50);
        } catch (err) {
            console.error('Error loading room messages:', err);
        }
    };

    createRoom = async () => {
        const input = document.getElementById('new-room-name');
        const name = input?.value.trim() ?? '';
        if (!name) return;
        try {
            await fetch('/api/chat-rooms', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });
        } catch (err) {
            console.error('Error creating room:', err);
        }
        if (input) input.value = '';
    };

    renameRoom = async () => {
        const input = document.getElementById('rename-room-input');
        const name = input?.value.trim() ?? '';
        if (!name || this.activeRoomId() == null) return;
        try {
            await fetch(`/api/chat-rooms/${this.activeRoomId()}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            });
        } catch (err) {
            console.error('Error renaming room:', err);
        }
    };

    deleteRoom = async () => {
        if (this.activeRoomId() == null) return;
        try {
            await fetch(`/api/chat-rooms/${this.activeRoomId()}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting room:', err);
        }
    };

    sendRoomMessage = async () => {
        if (!this.canPostToActiveRoom()) return;
        const text = this.messageText().trim();
        if (!text || this.activeRoomId() == null) return;
        try {
            await fetch('/api/chat-messages/room', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ roomId: this.activeRoomId(), content: text })
            });
        } catch (err) {
            console.error('Error sending message:', err);
        }
        this.messageText('');
    };

    onEnterKey = (data, event) => {
        if (event.key === 'Enter') this.sendRoomMessage();
        return true;
    };

    deleteMessage = async () => {
        const hidden = document.getElementById('delete-message-id');
        const id = parseInt(hidden?.value ?? '', 10);
        if (!id) return;
        try {
            await fetch(`/api/chat-messages/${id}`, { method: 'DELETE' });
        } catch (err) {
            console.error('Error deleting message:', err);
        }
    };

    openPrivateChat = (conv) => {
        this.activeRoomId(null);
        this.activeRoomName(null);
        this.chatMessages([]);
        this.privateMessages([]);
        this.activePrivateUserId(conv.userId());
        this.activePrivateUserName(conv.fullName());
        this.loadPrivateMessages(conv.userId());
    };

    loadPrivateMessages = async (otherUserId) => {
        try {
            const res = await fetch(`/api/chat-messages/private/${otherUserId}`);
            const data = await res.json();
            const myId = this.myUserId();
            this.privateMessages((data || []).map(m => new ChatMessageRow(m, myId)));
            setTimeout(() => scrollChatToBottom('chat-messages-private'), 50);
        } catch (err) {
            console.error('Error loading private messages:', err);
        }
    };

    sendPrivateMessage = async () => {
        const text = this.privateMessageText().trim();
        const toUserId = this.activePrivateUserId();
        if (!text || !toUserId) return;
        try {
            await this.hub.invoke('SendPrivate', toUserId, text);
        } catch (err) {
            console.error('Error sending private message:', err);
        }
        this.privateMessageText('');
    };

    onPrivateEnterKey = (data, event) => {
        if (event.key === 'Enter') this.sendPrivateMessage();
        return true;
    };

    startPrivateChatWith = (userId, fullName) => {
        let conv = this.privateConversations().find(c => c.userId() === userId);
        if (!conv) {
            conv = new PrivateConversationRow(userId, fullName);
            this.privateConversations.push(conv);
        }
        this.openPrivateChat(conv);
    };

    loadInitialRooms = async (initialPrivateUserId, initialPrivateUserName) => {
        try {
            const res = await fetch('/api/chat-rooms');
            const data = await res.json();
            this.chatRooms((data || []).map(d => new ChatRoomRow(d)));

            if (initialPrivateUserId) {
                this.startPrivateChatWith(initialPrivateUserId, initialPrivateUserName || initialPrivateUserId);
            } else if (this.chatRooms().length > 0) {
                this.joinRoom(this.chatRooms()[0]);
            }
        } catch (err) {
            console.error('Failed to load chat rooms:', err);
            this.onError('Failed to load chat rooms.');
        }
    };

    onGetProfileInfo = (data) => {
        this.myProfile(new ChatProfileInfo(data));
        this.myUserId(data.userId);
        this.isLoading(false);
    };

    onNewMessage = (data) => {
        if (this.activeRoomId() === data.toRoomId) {
            this.chatMessages.push(new ChatMessageRow(data, this.myUserId()));
            setTimeout(() => scrollChatToBottom('chat-messages-room'), 30);
        }
    };

    onNewPrivateMessage = (data) => {
        const me = this.myUserId();
        const otherId = data.fromUserId === me ? data.toUserId : data.fromUserId;
        const otherName = data.fromUserId === me
            ? (this.activePrivateUserName() || otherId)
            : (data.fromFullName || data.fromUserName || otherId);

        let conv = this.privateConversations().find(c => c.userId() === otherId);
        if (!conv) {
            conv = new PrivateConversationRow(otherId, otherName);
            this.privateConversations.push(conv);
        }

        if (this.activePrivateUserId() === otherId) {
            this.privateMessages.push(new ChatMessageRow(data, me));
            setTimeout(() => scrollChatToBottom('chat-messages-private'), 30);
        }
    };

    onAddUser = (data) => {
        if (!this.onlineUsers().some(u => u.userId() === data.userId))
            this.onlineUsers.push(new OnlineUserRow(data));
    };

    onRemoveUser = (data) => {
        this.onlineUsers.remove(u => u.userId() === data.userId);
    };

    onAddChatRoom = (data) => {
        if (!this.chatRooms().some(r => r.id() === data.id))
            this.chatRooms.push(new ChatRoomRow(data));
    };

    onUpdateChatRoom = (data) => {
        const room = this.chatRooms().find(r => r.id() === data.id);
        if (room) room.name(data.name);
        if (this.activeRoomId() === data.id) this.activeRoomName(data.name);
    };

    onRemoveChatRoom = (id) => {
        this.chatRooms.remove(r => r.id() === id);
        if (this.activeRoomId() === id) {
            this.activeRoomId(null);
            this.activeRoomName(null);
            this.chatMessages([]);
            const first = this.chatRooms()[0];
            if (first) this.joinRoom(first);
        }
    };

    onRemoveChatMessage = (id) => {
        this.chatMessages.remove(m => m.id() === id);
    };

    onError = (msg) => {
        this.serverInfoMessage(msg);
        const el = document.getElementById('chat-error-alert');
        if (el) {
            el.classList.remove('d-none');
            el.style.display = '';
            setTimeout(() => el.classList.add('d-none'), 5000);
        }
    };
}

document.addEventListener('DOMContentLoaded', async () => {
    const hub = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    const vm = new ChatViewModel(hub);
    ko.applyBindings(vm);

    hub.on('getProfileInfo', vm.onGetProfileInfo);
    hub.on('newMessage', vm.onNewMessage);
    hub.on('newPrivateMessage', vm.onNewPrivateMessage);
    hub.on('addUser', vm.onAddUser);
    hub.on('removeUser', vm.onRemoveUser);
    hub.on('addChatRoom', vm.onAddChatRoom);
    hub.on('updateChatRoom', vm.onUpdateChatRoom);
    hub.on('removeChatRoom', vm.onRemoveChatRoom);
    hub.on('removeChatMessage', vm.onRemoveChatMessage);
    hub.on('onRoomDeleted', () => {
        const first = vm.chatRooms()[0];
        if (first) vm.joinRoom(first);
        else {
            vm.activeRoomId(null);
            vm.activeRoomName(null);
            vm.chatMessages([]);
        }
    });
    hub.on('onError', vm.onError);

    const initialUserId = typeof chatInitialPrivateUserId !== 'undefined' ? chatInitialPrivateUserId : null;
    const initialUserName = typeof chatInitialPrivateUserName !== 'undefined' ? chatInitialPrivateUserName : null;

    try {
        await hub.start();
        if (vm.isLoading()) vm.isLoading(false);
        await vm.loadInitialRooms(initialUserId, initialUserName);
    } catch (err) {
        console.error('SignalR connection error:', err);
        vm.onError('SignalR connection failed.');
    }

    document.addEventListener('show.bs.modal', (e) => {
        if (e.target.id === 'remove-message-modal') {
            const msgId = e.relatedTarget?.getAttribute('data-message-id');
            const hidden = document.getElementById('delete-message-id');
            if (hidden && msgId) hidden.value = msgId;
        }
        if (e.target.id === 'rename-room-modal') {
            const input = document.getElementById('rename-room-input');
            if (input) input.value = vm.activeRoomName() || '';
        }
    });

    document.getElementById('expand-users-list')?.addEventListener('click', () => {
        const panel = document.getElementById('chat-users-panel');
        if (panel) panel.style.display = panel.style.display === 'none' ? '' : 'none';
    });
});
