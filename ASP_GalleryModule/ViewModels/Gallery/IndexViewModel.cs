using ASP_GalleryModule.Models.Gallery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASP_GalleryModule.ViewModels.Gallery
{
    public class IndexViewModel
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public List<Models.Gallery.Gallery> Galleries { get; set; }
        public List<GalleryImage> GalleryImages { get; set; }
    }
}
