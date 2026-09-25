using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Services;
using NzbDrone.Core.MediaFiles.BookImport.Manual;

namespace Chaptarr.Core.Test.MediaFiles.BookImport
{
    [TestFixture]
    public class ManualImportUnresolvableSuggestionFixture
    {
        private class AuthorProxy : DispatchProxy
        {
            public Author Author { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IAuthorService.FindByProviderId))
                {
                    return Author;
                }

                throw new NotImplementedException($"IAuthorService.{targetMethod?.Name}");
            }
        }

        private class BookProxy : DispatchProxy
        {
            public List<Book> Books { get; set; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IBookService.FindAllByWorkProviderId))
                {
                    return Books;
                }

                throw new NotImplementedException($"IBookService.{targetMethod?.Name}");
            }
        }

        private static string Check(Author author, List<Book> books, ManualImportItem item, string path = "/downloads/x/file.epub")
        {
            var authors = DispatchProxy.Create<IAuthorService, AuthorProxy>();
            ((AuthorProxy)(object)authors).Author = author;
            var bookService = DispatchProxy.Create<IBookService, BookProxy>();
            ((BookProxy)(object)bookService).Books = books;

            var service = new ManualImportService(
                null, null, null, null, null, authors, bookService, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                LogManager.GetCurrentClassLogger());

            var method = typeof(ManualImportService).GetMethod("GetUnresolvableSuggestionReason", BindingFlags.Instance | BindingFlags.NonPublic);
            return (string)method.Invoke(service, new object[] { item, path });
        }

        private static ManualImportItem Suggestion(string editionId = null)
        {
            return new ManualImportItem
            {
                SuggestedForeignAuthorId = "hc:162031",
                SuggestedForeignBookId = "hc:781589",
                SuggestedForeignEditionId = editionId
            };
        }

        [Test]
        public void work_missing_from_the_catalog_without_an_edition_id_should_be_flagged()
        {
            var reason = Check(new Author { Id = 7, Name = "Jonathan Strahan" }, new List<Book>(), Suggestion());

            Assert.That(reason, Does.Contain("cannot be added from metadata"));
            Assert.That(reason, Does.Contain("Jonathan Strahan"));
        }

        [Test]
        public void work_already_in_the_catalog_should_not_be_flagged()
        {
            var books = new List<Book> { new Book { Id = 1, AuthorId = 7, MediaType = BookMediaType.Ebook } };

            Assert.That(Check(new Author { Id = 7, Name = "Jonathan Strahan" }, books, Suggestion()), Is.Null);
        }

        [Test]
        public void suggestion_with_an_edition_id_should_not_be_flagged()
        {
            Assert.That(Check(new Author { Id = 7, Name = "Jonathan Strahan" }, new List<Book>(), Suggestion("hc:5")), Is.Null);
        }

        [Test]
        public void suggestion_for_an_author_that_is_not_local_yet_should_not_be_flagged()
        {
            Assert.That(Check(null, new List<Book>(), Suggestion()), Is.Null);
        }
    }
}
