using ASP_GalleryModule.Models.Gallery;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASP_GalleryModule.ViewModels.Gallery
{
    public class ViewGalleryViewModel
    {
        public Guid GalleryId { get; set; }

        public string GalleryTitle { get; set; }
        public string GalleryDescription { get; set; }
        public DateTime GalleryDate { get; set; }
        public string UserName { get; set; }

        [Display(Name = "Загрузить изображение")]
        public GalleryImage GalleryImage { get; set; }
        public List<GalleryImage> GalleryImages { get; set; }
        public int ImagesCount { get; set; }
    }
}
