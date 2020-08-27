using ASP_GalleryModule.Models.Gallery;
using System.Collections.Generic;

namespace ASP_GalleryModule.ViewModels.Gallery
{
    public class IndexViewModel
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public List<Models.Gallery.Gallery> Galleries { get; set; }
    }
}
