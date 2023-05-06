using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs.Memory.Scopes;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Search.Common.Models;

namespace SearchBot
{
    /// <summary>
    /// An empty bot handler.
    /// You can add your customization code here to extend your bot logic if needed.
    /// </summary>
    public class TeamsBot : TeamsActivityHandler
    {
        private readonly IConfiguration _configuration;

        public TeamsBot(IConfiguration configuration)
        {
            this._configuration = configuration;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string uri = _configuration["ApiEndpoint"];

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(uri);

            Question question = new Question
            {
                Prompt = turnContext.Activity.Text
            };

            var result = await client.PostAsJsonAsync<Question>("search", question);

            var response = await result.Content.ReadFromJsonAsync<Answer>();

            await turnContext.SendActivityAsync(MessageFactory.Text(response.Content));
        }
    }
}
