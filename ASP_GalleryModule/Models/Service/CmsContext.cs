using ASP_GalleryModule.Models.Gallery;
using Microsoft.EntityFrameworkCore;

namespace ASP_GalleryModule.Models.Service
{
    public class CmsContext : DbContext
    {
        public DbSet<Gallery.Gallery> Galleries { get; set; }
        public DbSet<GalleryImage> GalleryImages { get; set; }

        public CmsContext(DbContextOptions<CmsContext> options) : base(options)
        {
            Database.EnsureCreated();
        }
    }
}
