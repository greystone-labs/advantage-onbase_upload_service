using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public interface ISessionFactory
{
    public Application CreateApplication();
}
