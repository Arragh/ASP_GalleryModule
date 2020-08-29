using ASP_GalleryModule.Models.Gallery;
using ASP_GalleryModule.Models.Service;
using ASP_GalleryModule.ViewModels.Gallery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ASP_GalleryModule.Controllers
{
    public class GalleryController : Controller
    {
        CmsContext cmsDB;
        IWebHostEnvironment _appEnvironment;

        public GalleryController(CmsContext context, IWebHostEnvironment appEnvironment)
        {
            cmsDB = context;
            _appEnvironment = appEnvironment;
        }

        #region Главная страница галереи
        public async Task<IActionResult> Index(int pageNumber = 1)
        {
            // Количество записей на страницу
            int pageSize = 12;

            // Формируем список записей для обработки перед выводом на страницу
            IQueryable<Gallery> source = cmsDB.Galleries;

            // Рассчитываем, какие именно записи будут выведены на странице
            List<Gallery> galleries = await source.OrderByDescending(n => n.GalleryDate).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            // Общее количество записей для дальнейшего рассчета количества страниц
            int galleriesCount = await source.CountAsync();

            // Создаем массив Id-шников записей для выборки изображений к ним
            Guid[] galleryIdArray = galleries.Select(n => n.Id).ToArray();

            // Создаём модель для вывода на странице и кладём в неё все необходимые данные
            IndexViewModel model = new IndexViewModel()
            {
                Galleries = galleries,
                CurrentPage = pageNumber,
                TotalPages = (int)Math.Ceiling(galleriesCount / (double)pageSize)
            };

            // Выводим модель в представление
            return View(model);
        }
        #endregion

        #region Создать галерею [GET]
        [HttpGet]
        public IActionResult AddGallery()
        {
            return View();
        }
        #endregion

        #region Создать галерею [POST]
        [HttpPost]
        public async Task<IActionResult> AddGallery(AddGalleryViewModel model, IFormFile previewImage)
        {
            // Проверяем, чтобы входящий файл был не NULL и имел допустимый размер в мегабайтах
            if (previewImage != null && previewImage.Length > 2097152)
            {
                ModelState.AddModelError("GalleryPreviewImage", $"Файл \"{previewImage.FileName}\" превышает установленный лимит 2MB.");
            }

            if (ModelState.IsValid)
            {
                // Если размер входного файла больше 0, заходим в тело условия
                if (previewImage != null && previewImage.Length > 0)
                {
                    // Создаем новый объект класса FileInfo из полученного изображения для дальнейшей обработки
                    FileInfo imgFile = new FileInfo(previewImage.FileName);
                    // Приводим расширение к нижнему регистру (если оно было в верхнем)
                    string imgExtension = imgFile.Extension.ToLower();
                    // Генерируем новое имя для файла
                    string newFileName = Guid.NewGuid() + imgExtension;
                    // Пути сохранения файла
                    string pathPreview = "/files/images/preview/" + newFileName; // уменьшенное изображение

                    // В операторе try/catch делаем уменьшенную копию изображения.
                    // Если входным файлом окажется не изображение, нас перекинет в блок CATCH и выведет сообщение об ошибке
                    try
                    {
                        // Создаем объект класса SixLabors.ImageSharp.Image и грузим в него полученное изображение
                        using (Image image = Image.Load(previewImage.OpenReadStream()))
                        {
                            // Создаем уменьшенную копию и обрезаем её
                            var clone = image.Clone(x => x.Resize(new ResizeOptions
                            {
                                Mode = ResizeMode.Crop,
                                Size = new Size(300, 200)
                            }));
                            // Сохраняем уменьшенную копию
                            await clone.SaveAsync(_appEnvironment.WebRootPath + pathPreview, new JpegEncoder { Quality = 50 });
                        }
                    }
                    // Если вдруг что-то пошло не так (например, на вход подало не картинку), то выводим сообщение об ошибке
                    catch
                    {
                        // Создаем сообщение об ошибке для вывода пользователю
                        ModelState.AddModelError("GalleryPreviewImage", $"Файл {previewImage.FileName} имеет неверный формат.");

                        // Возвращаем модель с сообщением об ошибке в представление
                        return View(model);
                    }

                    Gallery gallery = new Gallery()
                    {
                        Id = Guid.NewGuid(),
                        GalleryTitle = model.GalleryTitle,
                        GalleryDescription = model.GalleryDescription,
                        GalleryDate = DateTime.Now,
                        UserName = "Mnemonic", // Хардкод. Потом обязательно заменить !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        GalleryPreviewImage = pathPreview
                    };

                    await cmsDB.Galleries.AddAsync(gallery);
                    await cmsDB.SaveChangesAsync();

                    return RedirectToAction("Index", "Gallery");
                }
                // Если не была выбрана картинка для превью, заходим в блок ELSE
                else
                {
                    Gallery gallery = new Gallery()
                    {
                        Id = Guid.NewGuid(),
                        GalleryTitle = model.GalleryTitle,
                        GalleryDescription = model.GalleryDescription,
                        GalleryDate = DateTime.Now,
                        UserName = "Mnemonic", // Хардкод. Потом обязательно заменить !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                        // Вбиваем картинку-заглушку
                        GalleryPreviewImage = "/files/images/preview/nopreview.jpg" // Хардкод. Потом обязательно заменить !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    };

                    await cmsDB.Galleries.AddAsync(gallery);
                    await cmsDB.SaveChangesAsync();

                    return RedirectToAction("Index", "Gallery");
                }
            }

            // Возврат модели при неудачной валидации
            return View(model);
        }
        #endregion

        #region Просмотр галереи / Добавление изображений [GET]
        [HttpGet]
        public async Task<IActionResult> ViewGallery(Guid galleryId, string imageToDeleteName = null)
        {
            // Если есть изображение, которое надо удалить, заходим в тело условия
            if (imageToDeleteName != null)
            {
                // Создаем экземпляр класса картинки и присваиваем ему данные из БД
                GalleryImage galleryImage = await cmsDB.GalleryImages.FirstOrDefaultAsync(i => i.ImageName == imageToDeleteName);

                // Делаем еще одну проверку. Лучше перебдеть. Если все ок, заходим в тело условия и удаляем изображения
                if (galleryImage != null)
                {
                    // Исходные (полноразмерные) изображения
                    FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathNormal);
                    if (imageNormal.Exists)
                    {
                        imageNormal.Delete();
                    }
                    // И их уменьшенные копии
                    FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathScaled);
                    if (imageScaled.Exists)
                    {
                        imageScaled.Delete();
                    }
                    // Удаляем информацию об изображениях из БД и сохраняем
                    cmsDB.GalleryImages.Remove(galleryImage);
                    await cmsDB.SaveChangesAsync();
                }
            }

            // Создаем экземпляр класса Gallery и присваиваем ему значения из БД
            Gallery gallery = await cmsDB.Galleries.FirstAsync(g => g.Id == galleryId);
            // Создаем список изображений из БД, закрепленных за выбранной галереей
            List<GalleryImage> images = await cmsDB.GalleryImages.Where(i => i.GalleryId == galleryId).OrderByDescending(i=>i.ImageDate).ToListAsync();

            // Создаем модель для передачи в представление и присваиваем значения
            ViewGalleryViewModel model = new ViewGalleryViewModel()
            {
                GalleryTitle = gallery.GalleryTitle,
                GalleryDescription = gallery.GalleryDescription,
                GalleryImages = images,
                // Скрытые поля
                GalleryId = galleryId,
                GalleryDate = gallery.GalleryDate,
                UserName = gallery.UserName,
                ImagesCount = images.Count
            };
            // Передаем модель в представление
            return View(model);
        }
        #endregion

        #region Просмотр галереи / Добавление изображений [POST]
        [HttpPost]
        public async Task<IActionResult> ViewGallery(ViewGalleryViewModel model, IFormFileCollection uploads)
        {
            List<GalleryImage> images = await cmsDB.GalleryImages.Where(i => i.GalleryId == model.GalleryId).OrderByDescending(i => i.ImageDate).ToListAsync();

            // Проверяем, не превышает ли количество загружаемых изображений допустимый лимит
            if (uploads.Count > Config.ImagesPerGallery - images.Count)
            {
                ModelState.AddModelError("GalleryImage", $"Вы пытаетесь загрузить {uploads.Count} изображений. Лимит галереи {Config.ImagesPerGallery} изображений. Вы можете загрузить еще {Config.ImagesPerGallery - images.Count} изображений.");
            }
            // Если всё в порядке, заходим в ELSE
            else
            {
                // Проверяем, чтобы размер файлов не превышал заданный объем
                foreach (var file in uploads)
                {
                    if (file.Length > 2097152)
                    {
                        ModelState.AddModelError("GalleryImage", $"Файл \"{file.FileName}\" превышает установленный лимит 2MB.");
                        break;
                    }
                }
            }

            // Если все в порядке, заходим в тело условия
            if (ModelState.IsValid)
            {
                // Далее начинаем обработку загружаемых изображений
                List<GalleryImage> galleryImages = new List<GalleryImage>();
                foreach (var uploadedImage in uploads)
                {
                    // Если размер входного файла больше 0, заходим в тело условия
                    if (uploadedImage.Length > 0)
                    {
                        // Создаем новый объект класса FileInfo из полученного изображения для дальнейшей обработки
                        FileInfo imgFile = new FileInfo(uploadedImage.FileName);
                        // Приводим расширение к нижнему регистру (если оно было в верхнем)
                        string imgExtension = imgFile.Extension.ToLower();
                        // Генерируем новое имя для файла
                        string newFileName = Guid.NewGuid() + imgExtension;
                        // Пути сохранения файла
                        string pathNormal = "/files/images/normal/" + newFileName; // изображение исходного размера
                        string pathScaled = "/files/images/scaled/" + newFileName; // уменьшенное изображение

                        // В операторе try/catch делаем уменьшенную копию изображения.
                        // Если входным файлом окажется не изображение, нас перекинет в блок CATCH и выведет сообщение об ошибке
                        try
                        {
                            // Создаем объект класса SixLabors.ImageSharp.Image и грузим в него полученное изображение
                            using (Image image = Image.Load(uploadedImage.OpenReadStream()))
                            {
                                // Создаем уменьшенную копию и обрезаем её
                                var clone = image.Clone(x => x.Resize(new ResizeOptions
                                {
                                    Mode = ResizeMode.Crop,
                                    Size = new Size(300, 200)
                                }));
                                // Сохраняем уменьшенную копию
                                await clone.SaveAsync(_appEnvironment.WebRootPath + pathScaled, new JpegEncoder { Quality = 50 });
                                // Сохраняем исходное изображение
                                await image.SaveAsync(_appEnvironment.WebRootPath + pathNormal);
                            }
                        }
                        // Если вдруг что-то пошло не так (например, на вход подало не картинку), то выводим сообщение об ошибке
                        catch
                        {
                            // Создаем сообщение об ошибке для вывода пользователю
                            ModelState.AddModelError("GalleryImage", $"Файл {uploadedImage.FileName} имеет неверный формат.");

                            // Удаляем только что созданные файлы (если ошибка возникла не на первом файле и некоторые уже были загружены на сервер)
                            foreach (var image in galleryImages)
                            {
                                // Исходные (полноразмерные) изображения
                                FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathNormal);
                                if (imageNormal.Exists)
                                {
                                    imageNormal.Delete();
                                }
                                // И их уменьшенные копии
                                FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + image.ImagePathScaled);
                                if (imageScaled.Exists)
                                {
                                    imageScaled.Delete();
                                }
                            }
                            // Возвращаем модель с сообщением об ошибке в представление
                            return View(model);
                        }

                        // Создаем объект класса GalleryImage со всеми параметрами
                        GalleryImage galleryImage = new GalleryImage()
                        {
                            Id = Guid.NewGuid(),
                            ImageName = newFileName,
                            ImagePathNormal = pathNormal,
                            ImagePathScaled = pathScaled,
                            GalleryId = model.GalleryId,
                            ImageDate = DateTime.Now
                        };
                        // Добавляем объект galleryImage в список galleryImages
                        galleryImages.Add(galleryImage);
                    }
                }

                // Если в процессе выполнения не возникло ошибок, сохраняем всё в БД
                if (galleryImages != null && galleryImages.Count > 0)
                {
                    await cmsDB.GalleryImages.AddRangeAsync(galleryImages);
                    await cmsDB.SaveChangesAsync();
                }

                // Выводим обновленную модель в представление
                return RedirectToAction("ViewGallery", "Gallery", new { galleryId = model.GalleryId });
            }

            // В случае, если произошла ошибка валидации, требуется заново присвоить список изображений и счетчик для возвращаемой модели
            // При перегонке модели из гет в пост, теряется список с изображениями. Причина пока не ясна, поэтому сделал такой костыль
            // Счетчик соответственно тоже обнулялся, поэтому его тоже приходится переназначать заново
            model.GalleryImages = images;
            model.ImagesCount = images.Count;

            // Возврат модели в представление в случае, если запорится валидация
            return View(model);
        }
        #endregion

        #region Редактировать галерею [GET]
        [HttpGet]
        public async Task<IActionResult> EditGallery(Guid galleryId)
        {
            Gallery gallery = await cmsDB.Galleries.FirstAsync(g => g.Id == galleryId);

            EditGalleryViewModel model = new EditGalleryViewModel()
            {
                GalleryId = galleryId,
                GalleryTitle = gallery.GalleryTitle,
                GalleryDescription = gallery.GalleryDescription,
                GalleryPreviewImage = gallery.GalleryPreviewImage
            };

            return View(model);
        }
        #endregion

        #region Редактировать галерею [POST]
        [HttpPost]
        public async Task<IActionResult> EditGallery(EditGalleryViewModel model, IFormFile previewImage)
        {
            if (previewImage != null && previewImage.Length > 2097152)
            {
                ModelState.AddModelError("GalleryPreviewImage", $"Файл \"{previewImage.FileName}\" превышает установленный лимит 2MB.");
            }

            if (ModelState.IsValid)
            {
                // Если исходный файл не равен NULL и его размер больше 0, заходим в тело условия
                if (previewImage != null && previewImage.Length > 0)
                {
                    // Создаем новый объект класса FileInfo из полученного изображения для дальнейшей обработки
                    FileInfo imgFile = new FileInfo(previewImage.FileName);
                    // Приводим расширение к нижнему регистру (если оно было в верхнем)
                    string imgExtension = imgFile.Extension.ToLower();
                    // Генерируем новое имя для файла
                    string newFileName = Guid.NewGuid() + imgExtension;
                    // Пути сохранения файла
                    string pathPreview = "/files/images/preview/" + newFileName; // уменьшенное изображение

                    // В операторе try/catch делаем уменьшенную копию изображения.
                    // Если входным файлом окажется не изображение, нас перекинет в блок CATCH и выведет сообщение об ошибке
                    try
                    {
                        // Создаем объект класса SixLabors.ImageSharp.Image и грузим в него полученное изображение
                        using (Image image = Image.Load(previewImage.OpenReadStream()))
                        {
                            // Создаем уменьшенную копию и обрезаем её
                            var clone = image.Clone(x => x.Resize(new ResizeOptions
                            {
                                Mode = ResizeMode.Crop,
                                Size = new Size(300, 200)
                            }));
                            // Сохраняем уменьшенную копию
                            await clone.SaveAsync(_appEnvironment.WebRootPath + pathPreview, new JpegEncoder { Quality = 50 });
                        }
                    }
                    // Если вдруг что-то пошло не так (например, на вход подало не картинку), то выводим сообщение об ошибке
                    catch
                    {
                        // Создаем сообщение об ошибке для вывода пользователю
                        ModelState.AddModelError("GalleryPreviewImage", $"Файл {previewImage.FileName} имеет неверный формат.");

                        // Возвращаем модель с сообщением об ошибке в представление
                        return View(model);
                    }

                    Gallery gallery = await cmsDB.Galleries.FirstAsync(g => g.Id == model.GalleryId);

                    // Если до этого превьюшка была не по дефолту, удаляем её с сервера
                    if (gallery.GalleryPreviewImage != "/files/images/preview/nopreview.jpg") // Хардкод. Потом обязательно заменить !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    {
                        FileInfo imageToDelete = new FileInfo(_appEnvironment.WebRootPath + gallery.GalleryPreviewImage);
                        if (imageToDelete.Exists)
                        {
                            imageToDelete.Delete();
                        }
                    }

                    // Обновляем значения на полученные с формы
                    gallery.GalleryTitle = model.GalleryTitle;
                    gallery.GalleryDescription = model.GalleryDescription;
                    gallery.GalleryPreviewImage = pathPreview;


                    // Сохраняем изменения в БД
                    cmsDB.Galleries.Update(gallery);
                    await cmsDB.SaveChangesAsync();

                    return RedirectToAction("Index", "Gallery");
                }
                // Если не была выбрана картинка для превью, заходим в блок ELSE
                else
                {
                    Gallery gallery = await cmsDB.Galleries.FirstAsync(g => g.Id == model.GalleryId);

                    gallery.GalleryTitle = model.GalleryTitle;
                    gallery.GalleryDescription = model.GalleryDescription;

                    cmsDB.Galleries.Update(gallery);
                    await cmsDB.SaveChangesAsync();

                    return RedirectToAction("Index", "Gallery");
                }
            }

            // Возврат модели при неудачной валидации
            return View(model);
        }
        #endregion

        #region Удалить галерею [GET]
        public async Task<IActionResult> DeleteGallery(Guid galleryId, bool isChecked)
        {
            if (isChecked)
            {
                Gallery gallery = await cmsDB.Galleries.FirstAsync(g => g.Id == galleryId);
                List<GalleryImage> galleryImages = await cmsDB.GalleryImages.Where(i => i.GalleryId == galleryId).ToListAsync();

                if (galleryImages.Count > 0)
                {
                    foreach (var galleryImage in galleryImages)
                    {
                        // Делаем еще одну проверку. Лучше перебдеть. Если все ок, заходим в тело условия и удаляем изображения
                        if (galleryImage != null)
                        {
                            // Исходные (полноразмерные) изображения
                            FileInfo imageNormal = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathNormal);
                            if (imageNormal.Exists)
                            {
                                imageNormal.Delete();
                            }
                            // И их уменьшенные копии
                            FileInfo imageScaled = new FileInfo(_appEnvironment.WebRootPath + galleryImage.ImagePathScaled);
                            if (imageScaled.Exists)
                            {
                                imageScaled.Delete();
                            }
                            // Удаляем информацию об изображениях из БД и сохраняем
                            cmsDB.GalleryImages.Remove(galleryImage);
                        }
                    }
                }

                // Удаляем превью-изображение (если оно не по дефолту)
                if (gallery.GalleryPreviewImage != "/files/images/preview/nopreview.jpg") // Хардкод. Потом обязательно заменить !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                {
                    FileInfo previewImage = new FileInfo(_appEnvironment.WebRootPath + gallery.GalleryPreviewImage);
                    if (previewImage.Exists)
                    {
                        previewImage.Delete();
                    }
                }

                cmsDB.Galleries.Remove(gallery);
                await cmsDB.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Gallery");
        }
        #endregion

    }
}
