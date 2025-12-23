using Google.Ads.GoogleAds;
using Google.Ads.GoogleAds.Lib;
using Google.Ads.GoogleAds.Config;
using Google.Ads.GoogleAds.V20.Resources;
using Google.Ads.GoogleAds.V20.Services;
using static Google.Ads.GoogleAds.V20.Enums.AdvertisingChannelTypeEnum.Types;
using static Google.Ads.GoogleAds.V20.Enums.CampaignStatusEnum.Types;
using Google.Ads.GoogleAds.V20.Common;

public class GoogleAdsApiWrapper
{
    private readonly GoogleAdsClient client;
    private readonly string customerId;

    public GoogleAdsApiWrapper(string developerToken, string clientId, string clientSecret, string refreshToken, string customerId)
    {
        this.customerId = customerId;

        var config = new GoogleAdsConfig()
        {
            DeveloperToken = developerToken,
            OAuth2ClientId = clientId,
            OAuth2ClientSecret = clientSecret,
            OAuth2RefreshToken = refreshToken,
            LoginCustomerId = customerId
        };

        client = new GoogleAdsClient(config);
    }

    // 1. Get all campaigns
    public IEnumerable<Campaign> GetAllCampaigns()
    {
        var service = client.GetService(Services.V20.GoogleAdsService);
        var query = "SELECT campaign.id, campaign.name, campaign.status FROM campaign";
        var request = new SearchGoogleAdsRequest()
        {
            CustomerId = customerId,
            Query = query
        };

        var results = new List<Campaign>();
        foreach (var row in service.Search(request))
        {
            results.Add(row.Campaign);
        }

        return results;
    }

    // 2. Get ad groups for a campaign
    public IEnumerable<AdGroup> GetAdGroups(long campaignId)
    {
        var service = client.GetService(Services.V20.GoogleAdsService);
        var query = $"SELECT ad_group.id, ad_group.name FROM ad_group WHERE ad_group.campaign = 'customers/{customerId}/campaigns/{campaignId}'";
        var request = new SearchGoogleAdsRequest()
        {
            CustomerId = customerId,
            Query = query
        };

        var results = new List<AdGroup>();
        foreach (var row in service.Search(request))
        {
            results.Add(row.AdGroup);
        }

        return results;
    }

    // 3. Get keywords for a campaign
    public IEnumerable<KeywordView> GetKeywords()
    {
        var service = client.GetService(Services.V20.GoogleAdsService);
        var query = "SELECT keyword_view.resource_name, ad_group_criterion.keyword.text, ad_group_criterion.keyword.match_type " +
                    "FROM keyword_view " +
                    "WHERE ad_group_criterion.status = 'ENABLED'";

        var request = new SearchGoogleAdsRequest()
        {
            CustomerId = customerId,
            Query = query
        };

        var results = new List<KeywordView>();
        foreach (var row in service.Search(request))
        {
            results.Add(row.KeywordView);
        }

        return results;
    }

    // 4. Get performance stats
    public IEnumerable<GoogleAdsRow> GetCampaignPerformanceStats()
    {
        var service = client.GetService(Services.V20.GoogleAdsService);
        var query = @"SELECT campaign.id, campaign.name, metrics.impressions, metrics.clicks, metrics.cost_micros 
                      FROM campaign 
                      WHERE campaign.status = 'ENABLED' 
                      DURING LAST_30_DAYS";

        var request = new SearchGoogleAdsRequest()
        {
            CustomerId = customerId,
            Query = query
        };

        var results = new List<GoogleAdsRow>();
        foreach (var row in service.Search(request))
        {
            results.Add(row);
        }

        return results;
    }

    // 5. Create a campaign (minimal example)
    public Campaign CreateTestCampaign(string name)
    {
        var campaignService = client.GetService(Services.V20.CampaignService);

        var campaign = new Campaign()
        {
            Name = name,
            AdvertisingChannelType = AdvertisingChannelType.Search,
            Status = CampaignStatus.Paused,
            ManualCpc = new ManualCpc(),
            CampaignBudget = "customers/" + customerId + "/campaignBudgets/INSERT_BUDGET_ID" // You need a real budget ID here
        };

        var operation = new CampaignOperation()
        {
            Create = campaign
        };

        var response = campaignService.MutateCampaigns(customerId, new[] { operation });
        var createdCampaign = response.Results[0];
        Console.WriteLine($"Created campaign with resource name: {createdCampaign.ResourceName}");

        return campaign;
    }
}
