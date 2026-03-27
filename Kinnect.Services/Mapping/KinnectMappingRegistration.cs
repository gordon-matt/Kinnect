using Extenso.Mapping;

namespace Kinnect.Services.Mapping;

/// <summary>
/// Registers ExtensoMapper entity ↔ DTO mappings. Call from application startup and from tests that construct mapped repositories.
/// </summary>
public static class KinnectMappingRegistration
{
    private static bool registered;

    public static void Register()
    {
        if (registered)
        {
            return;
        }

        ExtensoMapper.Register<Post, PostDto>(x => x.ToDto());
        ExtensoMapper.Register<PostDto, Post>(x => x.ToEntity());

        ExtensoMapper.Register<Person, PersonDto>(x => x.ToDto());
        ExtensoMapper.Register<PersonDto, Person>(x => x.ToEntity());

        ExtensoMapper.Register<PersonEvent, PersonEventDto>(x => x.ToDto());
        ExtensoMapper.Register<PersonEventDto, PersonEvent>(x => x.ToEntity());

        ExtensoMapper.Register<Photo, PhotoDto>(x => x.ToDto());
        ExtensoMapper.Register<PhotoDto, Photo>(x => x.ToEntity());

        ExtensoMapper.Register<Video, VideoDto>(x => x.ToDto());
        ExtensoMapper.Register<VideoDto, Video>(x => x.ToEntity());

        ExtensoMapper.Register<Document, DocumentDto>(x => x.ToDto());
        ExtensoMapper.Register<DocumentDto, Document>(x => x.ToEntity());

        registered = true;
    }
}