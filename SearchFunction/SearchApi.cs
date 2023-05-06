using System.Net;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using SearchFunction.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.Orchestration;
using Search.Common.Models;

namespace SearchFunction
{
    public class SearchApi
    {
        private readonly ILogger<SearchApi> _logger;
        private readonly IConfiguration _configuration;
        private SearchClient client;
        private IKernel kernel;

        public SearchApi(ILogger<SearchApi> log, IConfiguration configuration)
        {
            _logger = log;
            _configuration = configuration;

            string endpoint = _configuration["AzureSearch:Endpoint"];
            string apiKey = _configuration["AzureSearch:ApiKey"];
            string indexName = _configuration["AzureSearch:IndexName"];

            string azureOpenAIKey = configuration["AzureOpenAI:ApiKey"];
            string deploymentName = configuration["AzureOpenAI:DeploymentName"];
            string openAIEndpoint = configuration["AzureOpenAI:Endpoint"];

            string openAIKey = _configuration["OpenAI:ApiKey"];

            kernel = Kernel.Builder.Build();
            //kernel.Config.AddAzureChatCompletionService("chatgpt-azure", deploymentName, openAIEndpoint, azureOpenAIKey, true);
            kernel.Config.AddOpenAIChatCompletionService("gpt-4", "gpt-4", openAIKey);

            AzureKeyCredential credential = new AzureKeyCredential(apiKey);
            client = new SearchClient(new Uri(endpoint), indexName, credential);
        }

        [FunctionName("Search")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Question), Description = "The question from the user")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The OK response")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var json = await req.ReadAsStringAsync();
            Question question = JsonConvert.DeserializeObject<Question>(json);

            string prompt = @"Below is a question asked by the user that needs to be answered by searching in a knowledge base about Star Wars characters, planets and veichles.
                            Generate a search query based on the question. 
                            Do not include cited source filenames and document names e.g info.txt or doc.pdf in the search query terms.
                            Do not include any text inside [] or <<>> in the search query terms.

                            ===== QUESTION =====
                            {{$input}}";

            var semanticFunction = kernel.CreateSemanticFunction(prompt);
            var response = await kernel.RunAsync(question.Prompt, semanticFunction);

            var searchResponse = await client.SearchAsync<DatabankEntry>(response.Result);
            var results = searchResponse.Value.GetResults();

            var entries = new List<DatabankEntry>();

            foreach (var searchResult in results)
            {
                var document = searchResult.Document;
                entries.Add(document);
            }

            string searchPrompt = @"You are an assistant who helps people with questions around Star Wars characthers, planets and veichles. Be brief in your answers.
            Answer ONLY with the facts listed in the list of sources below. If there isn't enough information below, say you don't know. Do not generate answers that don't use the sources below. 
            If asking a clarifying question to the user would help, ask the question.
            For tabular information return it as an html table. Do not return markdown format.
            Each source has a name followed by colon and the actual information, always include the source name for each fact you use in the response. 
            Use square brakets to reference the source, e.g. [info1.txt]. Don't combine sources, list each source separately, e.g. [info1.txt][info2.pdf].

            ---
            SOURCES
            {{$sources}}
            ---
            QUESTION
            {{$question}}"
            ;

            _logger.LogInformation(searchPrompt);

            string sources = string.Empty;
            foreach (var entry in entries)
            {
                sources +=$"{entry.FileName}: {entry.Content}\n\n";
            }

            var promptConfig = new PromptTemplateConfig
            {
                Completion =
                {
                    MaxTokens = 2000,
                    Temperature = 0.2,
                    TopP = 0.5,
                }
            };

            var searchPromptTemplate = new PromptTemplate(searchPrompt, promptConfig, kernel);
            var functionConfig = new SemanticFunctionConfig(promptConfig, searchPromptTemplate);

            var searchFunction = kernel.RegisterSemanticFunction("StarWars", "Search", functionConfig);
            var context = new ContextVariables();
            context.Set("sources", sources);
            context.Set("question", question.Prompt);

            var finalResponse = await kernel.RunAsync(context, searchFunction);

            Answer answer = new Answer
            {
                Content = finalResponse.Result
            };

            return new OkObjectResult(answer);
        }
    }
}

