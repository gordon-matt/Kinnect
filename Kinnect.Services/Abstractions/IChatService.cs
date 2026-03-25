namespace Kinnect.Services.Abstractions;

public interface IChatService
{
    Task<Result<IEnumerable<ChatRoomDto>>> GetRoomsAsync();

    Task<Result<ChatRoomDto>> GetRoomByIdAsync(int roomId);

    Task<Result<ChatRoomDto>> CreateRoomAsync(string name, string currentUserId);

    Task<Result<ChatRoomDto>> UpdateRoomAsync(int roomId, string name, string currentUserId);

    Task<Result<ChatDeleteRoomDto>> DeleteRoomAsync(int roomId, string currentUserId);

    Task<Result<IEnumerable<ChatMessageDto>>> GetRoomMessagesAsync(int roomId, int take);

    Task<Result<IEnumerable<ChatMessageDto>>> GetPrivateMessagesAsync(string currentUserId, string otherUserId, int take);

    Task<Result<ChatMessageDto>> CreateRoomMessageAsync(int roomId, string content, string currentUserId);

    Task<Result<ChatMessageDto>> CreatePrivateMessageAsync(string currentUserId, string toUserId, string content);

    Task<Result<ChatDeleteMessageDto>> DeleteMessageAsync(int messageId, string currentUserId);

    Task<Result<ChatUserDto>> GetCurrentChatUserAsync(string currentUserId, string fallbackUserName);

    Task<Result<ChatPrivateConversationTargetDto>> GetPrivateConversationTargetAsync(string userId);
}
