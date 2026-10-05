using HtmlAgilityPack;

const string url = "https://www.mariokart64.com/mkdd/course.php?cid=0";

using var httpClient = new HttpClient();

httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 (compatible; MKDDTracker/1.0)"
);

Console.WriteLine($"Téléchargement de : {url}");

var html = await httpClient.GetStringAsync(url);

Console.WriteLine($"HTML reçu : {html.Length:N0} caractères");

var document = new HtmlDocument();
document.LoadHtml(html);

Console.WriteLine();
Console.WriteLine($"Titre : {document.DocumentNode.SelectSingleNode("//title")?.InnerText}");
Console.WriteLine();

var tables = document.DocumentNode.SelectNodes("//table");

Console.WriteLine($"Nombre de tables trouvées : {tables?.Count ?? 0}");

if (tables is null)
{
    return;
}

for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
{
    Console.WriteLine();
    Console.WriteLine($"========== TABLE {tableIndex} ==========");

    var rows = tables[tableIndex].SelectNodes(".//tr");

    if (rows is null)
    {
        continue;
    }

    foreach (var row in rows)
    {
        var cells = row.SelectNodes("./th|./td");

        if (cells is null)
        {
            continue;
        }

        var values = cells
            .Select(cell => HtmlEntity.DeEntitize(cell.InnerText).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value));

        Console.WriteLine(string.Join(" | ", values));
    }
}