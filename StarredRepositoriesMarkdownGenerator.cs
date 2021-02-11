using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Internal;
using Octokit.GraphQL.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

/*
    <PackageReference Include="Octokit" Version="0.48.0" />
    <PackageReference Include="Octokit.GraphQL" Version="0.1.7-beta" />
 */
namespace AllStars.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string githubToken = args[0];
            string fullPathOutput = args[1];

            List<Repository> repositories = await GetStarredRepositoriesAsync(githubToken);
            await WriteMarkdown(repositories, fullPathOutput);
        }

        public static async Task<List<Repository>> GetStarredRepositoriesAsync(string authToken)
        {
            var productInformation = new ProductHeaderValue("lucamazzanti", "1.0.0");

            var connection = new Connection(productInformation, new InMemoryCredentialStore(authToken));

            var query = new Query()
                .Viewer.StarredRepositories(first: 100, after: new Variable("after"))
                .Select(conn => new
                {
                    conn.PageInfo.EndCursor,
                    conn.PageInfo.HasNextPage,
                    conn.TotalCount,
                    Items = conn.Nodes.Select(repository => new Repository
                    {
                        Name = repository.Name,
                        FullName = repository.NameWithOwner,
                        Description = repository.Description,
                        HtmlDescription = repository.DescriptionHTML,
                        HtmlUrl = repository.Url,
                        Stars = repository.Stargazers(null, null, null, null, null).TotalCount,
                        Language = repository.PrimaryLanguage.Select(language => language.Name).SingleOrDefault(),
                        Languages = repository.Languages(null, null, null, null,
                                    new Arg<LanguageOrder>(new LanguageOrder
                                    {
                                        Field = LanguageOrderField.Size,
                                        Direction = OrderDirection.Desc
                                    }))
                                    .AllPages().Select(language => language.Name).ToList().ToArray(),
                        IsPrivate = repository.IsPrivate,
                        Topics = repository.RepositoryTopics(null, null, null, null)
                                .AllPages().Select(topic => topic.Topic.Name).ToList().ToArray()
                    }).ToList(),
                }).Compile();

            var vars = new Dictionary<string, object>
            {
                { "after", null },
            };
            var queryResult = await connection.Run(query, vars);

            vars["after"] = queryResult.HasNextPage ? queryResult.EndCursor : null;
            while (vars["after"] != null)
            {
                var page = await connection.Run(query, vars);
                queryResult.Items.AddRange(page.Items);
                vars["after"] = page.HasNextPage ? page.EndCursor : null;
            }

            List<Repository> results = queryResult.Items.Where(i => !i.IsPrivate).ToList();

            return results;
        }

        public static string GenerateMarkdown(IEnumerable<Repository> repositories)
        {
            var sb = new StringBuilder();

            string lastLanguage = null;
            foreach (Repository result in repositories.OrderByDescending(i => i.Language ?? i.Languages.FirstOrDefault()))
            {
                string language = WebUtility.UrlEncode(result.Language ?? result.Languages.FirstOrDefault()) ?? "document";
                if (lastLanguage != language)
                {
                    sb.AppendLine();
                    sb.AppendLine($"{language} projects:");
                    lastLanguage = language;
                }

                sb.AppendLine($"- [{result.FullName}]({result.HtmlUrl}) {result.Description}");
            }

            return sb.ToString();
        }

        public static async Task WriteMarkdown(IEnumerable<Repository> repositories, string filePath)
        {
            string markdown = GenerateMarkdown(repositories);

            await File.WriteAllTextAsync(filePath, markdown);
        }

        public class Repository
        {
            public string Name { get; set; }

            public string FullName { get; set; }

            public string Description { get; set; }

            public string HtmlDescription { get; set; }

            public string HtmlUrl { get; set; }

            public int Stars { get; set; }

            public string Language { get; set; }

            public string[] Languages { get; set; }

            public string[] Topics { get; set; }

            public bool IsPrivate { get; set; }
        }
    }
}
