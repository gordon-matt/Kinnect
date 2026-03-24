'use strict';

(function () {
    // ── Helpers ──────────────────────────────────────────────────────────────

    function formatTimestamp(ts) {
        const d = new Date(ts);
        const now = new Date();
        const diffDays = Math.floor((now - d) / 86400000);
        const hh = d.getHours().toString().padStart(2, '0');
        const mm = d.getMinutes().toString().padStart(2, '0');
        if (diffDays === 0) return `${hh}:${mm}`;
        if (diffDays === 1) return `Yesterday ${hh}:${mm}`;
        return `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()} ${hh}:${mm}`;
    }

    function scrollToBottom(id) {
        const el = document.getElementById(id);
        if (el) el.scrollTop = el.scrollHeight;
    }

    // ── KO constructors ──────────────────────────────────────────────────────

    function ChatRoom(data) {
        this.id = ko.observable(data.id);
        this.name = ko.observable(data.name);
        this.adminUserId = ko.observable(data.adminUserId);
    }

    function ChatMessage(data, myUserId) {
        this.id = ko.observable(data.id);
        this.content = ko.observable(data.content);
        this.fromUserId = ko.observable(data.fromUserId);
        this.fromUserName = ko.observable(data.fromUserName || '');
        this.fromFullName = ko.observable(data.fromFullName || data.fromUserName || '?');
        this.timestamp = ko.observable(data.timestamp);
        this.timestampDisplay = ko.pureComputed(() => formatTimestamp(this.timestamp()));
        this.isMine = ko.observable(data.fromUserId === myUserId);
    }

    function OnlineUser(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
        this.currentRoom = ko.observable(data.currentRoom || '');
    }

    function PrivateConversation(userId, fullName) {
        this.userId = ko.observable(userId);
        this.fullName = ko.observable(fullName || userId);
    }

    function ProfileInfo(data) {
        this.userId = ko.observable(data.userId);
        this.userName = ko.observable(data.userName || '');
        this.fullName = ko.observable(data.fullName || data.userName || '?');
    }

    // ── ViewModel ────────────────────────────────────────────────────────────

    function ChatViewModel() {
        var self = this;

        self.isLoading = ko.observable(true);
        self.serverInfoMessage = ko.observable('');
        self.myProfile = ko.observable(null);
        self.myUserId = ko.observable(null);

        self.chatRooms = ko.observableArray([]);
        self.chatMessages = ko.observableArray([]);
        self.onlineUsers = ko.observableArray([]);
        self.privateConversations = ko.observableArray([]);
        self.privateMessages = ko.observableArray([]);

        self.activeRoomId = ko.observable(null);
        self.activeRoomName = ko.observable(null);
        self.activePrivateUserId = ko.observable(null);
        self.activePrivateUserName = ko.observable(null);

        self.messageText = ko.observable('');
        self.privateMessageText = ko.observable('');

        self.isRoomAdmin = ko.pureComputed(() => {
            const room = self.chatRooms().find(r => r.id() === self.activeRoomId());
            return room && self.myUserId() && room.adminUserId() === self.myUserId();
        });

        // ── Room actions ──────────────────────────────────────────────────

        self.joinRoom = function (room) {
            self.activePrivateUserId(null);
            self.activePrivateUserName(null);
            self.chatMessages([]);
            self.activeRoomId(room.id());
            self.activeRoomName(room.name());

            connection.invoke('Join', room.name()).catch(console.error);

            fetch(`/api/chat-messages/room/${room.id()}`)
                .then(r => r.json())
                .then(data => {
                    const msgs = (data || []).map(m => new ChatMessage(m, self.myUserId()));
                    self.chatMessages(msgs);
                    setTimeout(() => scrollToBottom('chat-messages-room'), 50);
                });
        };

        self.createRoom = function () {
            const name = document.getElementById('new-room-name').value.trim();
            if (!name) return;
            fetch('/api/chat-rooms', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            }).catch(console.error);
            document.getElementById('new-room-name').value = '';
        };

        self.renameRoom = function () {
            const name = document.getElementById('rename-room-input').value.trim();
            if (!name || self.activeRoomId() == null) return;
            fetch(`/api/chat-rooms/${self.activeRoomId()}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name })
            }).catch(console.error);
        };

        self.deleteRoom = function () {
            if (self.activeRoomId() == null) return;
            fetch(`/api/chat-rooms/${self.activeRoomId()}`, {
                method: 'DELETE'
            }).catch(console.error);
        };

        // ── Room message actions ──────────────────────────────────────────

        self.sendRoomMessage = function () {
            const text = self.messageText().trim();
            if (!text || self.activeRoomId() == null) return;
            fetch('/api/chat-messages/room', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ roomId: self.activeRoomId(), content: text })
            }).catch(console.error);
            self.messageText('');
        };

        self.onEnterKey = function (data, event) {
            if (event.keyCode === 13) self.sendRoomMessage();
            return true;
        };

        self.deleteMessage = function () {
            const id = parseInt(document.getElementById('delete-message-id').value, 10);
            if (!id) return;
            fetch(`/api/chat-messages/${id}`, { method: 'DELETE' }).catch(console.error);
        };

        // ── Private message actions ───────────────────────────────────────

        self.openPrivateChat = function (conv) {
            self.activeRoomId(null);
            self.activeRoomName(null);
            self.chatMessages([]);
            self.privateMessages([]);
            self.activePrivateUserId(conv.userId());
            self.activePrivateUserName(conv.fullName());

            fetch(`/api/chat-messages/private/${conv.userId()}`)
                .then(r => r.json())
                .then(data => {
                    const msgs = (data || []).map(m => new ChatMessage(m, self.myUserId()));
                    self.privateMessages(msgs);
                    setTimeout(() => scrollToBottom('chat-messages-private'), 50);
                });
        };

        self.sendPrivateMessage = function () {
            const text = self.privateMessageText().trim();
            const toUserId = self.activePrivateUserId();
            if (!text || !toUserId) return;
            connection.invoke('SendPrivate', toUserId, text).catch(console.error);
            self.privateMessageText('');
        };

        self.onPrivateEnterKey = function (data, event) {
            if (event.keyCode === 13) self.sendPrivateMessage();
            return true;
        };

        // ── Open private chat by userId (for "Send Message" from profile) ─

        self.startPrivateChatWith = function (userId, fullName) {
            let conv = self.privateConversations().find(c => c.userId() === userId);
            if (!conv) {
                conv = new PrivateConversation(userId, fullName);
                self.privateConversations.push(conv);
            }
            self.openPrivateChat(conv);
        };

        // ── Hub event handlers ────────────────────────────────────────────

        self.onGetProfileInfo = function (data) {
            self.myProfile(new ProfileInfo(data));
            self.myUserId(data.userId);
            self.isLoading(false);
        };

        self.onNewMessage = function (data) {
            if (self.activeRoomId() === data.toRoomId) {
                self.chatMessages.push(new ChatMessage(data, self.myUserId()));
                setTimeout(() => scrollToBottom('chat-messages-room'), 30);
            }
        };

        self.onNewPrivateMessage = function (data) {
            const otherId = data.fromUserId === self.myUserId() ? data.toUserId : data.fromUserId;
            const otherName = data.fromUserId === self.myUserId()
                ? (self.activePrivateUserName() || otherId)
                : (data.fromFullName || data.fromUserName || otherId);

            let conv = self.privateConversations().find(c => c.userId() === otherId);
            if (!conv) {
                conv = new PrivateConversation(otherId, otherName);
                self.privateConversations.push(conv);
            }

            if (self.activePrivateUserId() === otherId) {
                self.privateMessages.push(new ChatMessage(data, self.myUserId()));
                setTimeout(() => scrollToBottom('chat-messages-private'), 30);
            }
        };

        self.onAddUser = function (data) {
            if (!self.onlineUsers().some(u => u.userId() === data.userId))
                self.onlineUsers.push(new OnlineUser(data));
        };

        self.onRemoveUser = function (data) {
            self.onlineUsers.remove(u => u.userId() === data.userId);
        };

        self.onAddChatRoom = function (data) {
            if (!self.chatRooms().some(r => r.id() === data.id))
                self.chatRooms.push(new ChatRoom(data));
        };

        self.onUpdateChatRoom = function (data) {
            const room = self.chatRooms().find(r => r.id() === data.id);
            if (room) room.name(data.name);
            if (self.activeRoomId() === data.id) self.activeRoomName(data.name);
        };

        self.onRemoveChatRoom = function (id) {
            self.chatRooms.remove(r => r.id() === id);
            if (self.activeRoomId() === id) {
                self.activeRoomId(null);
                self.activeRoomName(null);
                self.chatMessages([]);
                const first = self.chatRooms()[0];
                if (first) self.joinRoom(first);
            }
        };

        self.onRemoveChatMessage = function (id) {
            self.chatMessages.remove(m => m.id() === id);
        };

        self.onError = function (msg) {
            self.serverInfoMessage(msg);
            const el = document.getElementById('chat-error-alert');
            if (el) {
                el.classList.remove('d-none');
                el.style.display = '';
                setTimeout(() => { el.classList.add('d-none'); }, 5000);
            }
        };
    }

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    var viewModel = new ChatViewModel();
    ko.applyBindings(viewModel);

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/chatHub')
        .withAutomaticReconnect()
        .build();

    connection.on('getProfileInfo', viewModel.onGetProfileInfo);
    connection.on('newMessage', viewModel.onNewMessage);
    connection.on('newPrivateMessage', viewModel.onNewPrivateMessage);
    connection.on('addUser', viewModel.onAddUser);
    connection.on('removeUser', viewModel.onRemoveUser);
    connection.on('addChatRoom', viewModel.onAddChatRoom);
    connection.on('updateChatRoom', viewModel.onUpdateChatRoom);
    connection.on('removeChatRoom', viewModel.onRemoveChatRoom);
    connection.on('removeChatMessage', viewModel.onRemoveChatMessage);
    connection.on('onRoomDeleted', () => {
        const first = viewModel.chatRooms()[0];
        if (first) viewModel.joinRoom(first);
        else { viewModel.activeRoomId(null); viewModel.activeRoomName(null); viewModel.chatMessages([]); }
    });
    connection.on('onError', viewModel.onError);

    connection.start()
        .then(() => {
            // Load room list
            fetch('/api/chat-rooms')
                .then(r => r.json())
                .then(data => {
                    viewModel.chatRooms((data || []).map(d => new ChatRoom(d)));

                    // If redirected from a profile page, open private chat
                    if (chatInitialPrivateUserId) {
                        viewModel.startPrivateChatWith(chatInitialPrivateUserId, chatInitialPrivateUserName || chatInitialPrivateUserId);
                    } else if (viewModel.chatRooms().length > 0) {
                        viewModel.joinRoom(viewModel.chatRooms()[0]);
                    }
                });
        })
        .catch(err => console.error('SignalR connection error:', err));

    // Delete message modal: capture message id
    document.addEventListener('show.bs.modal', function (e) {
        if (e.target.id === 'remove-message-modal') {
            const msgId = e.relatedTarget?.getAttribute('data-message-id');
            const hidden = document.getElementById('delete-message-id');
            if (hidden && msgId) hidden.value = msgId;
        }
        if (e.target.id === 'rename-room-modal') {
            const input = document.getElementById('rename-room-input');
            if (input) input.value = viewModel.activeRoomName() || '';
        }
    });

    // Toggle users panel
    document.getElementById('expand-users-list')?.addEventListener('click', () => {
        const panel = document.getElementById('chat-users-panel');
        if (panel) panel.style.display = panel.style.display === 'none' ? '' : 'none';
    });
})();
