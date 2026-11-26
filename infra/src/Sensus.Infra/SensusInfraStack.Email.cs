using System.Globalization;
using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.SES;

namespace Sensus.Infra;

/// <summary>
/// Monitoreo de rebotes, quejas y rechazos de Amazon SES en CloudWatch (Spec 029, FR-029-14).
/// </summary>
public sealed partial class SensusInfraStack
{
    private void CreateSesMonitoring(string envName, IAlarmAction alarmAction)
    {
        var configSetName = $"sensus-transactional-{envName}";

        var configSet = new CfnConfigurationSet(this, "SensusSesConfigurationSet", new CfnConfigurationSetProps
        {
            Name = configSetName
        });

        var eventDestination = new CfnConfigurationSetEventDestination(this, "SensusSesCloudWatchDestination", new CfnConfigurationSetEventDestinationProps
        {
            ConfigurationSetName = configSetName,
            EventDestination = new CfnConfigurationSetEventDestination.EventDestinationProperty
            {
                Name = "cloudwatch-reputation-metrics",
                Enabled = true,
                MatchingEventTypes = ["BOUNCE", "COMPLAINT", "REJECT", "DELIVERY"],
                CloudWatchDestination = new CfnConfigurationSetEventDestination.CloudWatchDestinationProperty
                {
                    DimensionConfigurations = new CfnConfigurationSetEventDestination.DimensionConfigurationProperty[]
                    {
                        new()
                        {
                            DimensionName = "ses:configuration-set",
                            DimensionValueSource = "messageTag",
                            DefaultDimensionValue = configSetName
                        }
                    }
                }
            }
        });
        eventDestination.AddResourceDependency(configSet);

        var threshold = double.TryParse(
            Node.TryGetContext("sesBounceComplaintThreshold")?.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedThreshold)
            ? parsedThreshold
            : 5.0;

        var bounceMetric = new Metric(new MetricProps
        {
            Namespace = "AWS/SES",
            MetricName = "Reputation.BounceCount",
            DimensionsMap = new Dictionary<string, string>
            {
                ["ses:configuration-set"] = configSetName
            },
            Period = Duration.Minutes(5),
            Statistic = "Sum"
        });

        var complaintMetric = new Metric(new MetricProps
        {
            Namespace = "AWS/SES",
            MetricName = "Reputation.ComplaintCount",
            DimensionsMap = new Dictionary<string, string>
            {
                ["ses:configuration-set"] = configSetName
            },
            Period = Duration.Minutes(5),
            Statistic = "Sum"
        });

        var sesReputationMetric = new MathExpression(new MathExpressionProps
        {
            Expression = "bounces + complaints",
            UsingMetrics = new Dictionary<string, IMetric>
            {
                ["bounces"] = bounceMetric,
                ["complaints"] = complaintMetric
            },
            Period = Duration.Minutes(5),
            Label = "Rebotes y quejas SES (5 min)"
        });

        // 8va alarma estándar dentro de las 10 gratuitas de CloudWatch Free Tier.
        var sesReputationAlarm = new Alarm(this, "SensusSesReputationAlarm", new AlarmProps
        {
            AlarmName = $"sensus-ses-bounces-complaints-{envName}",
            AlarmDescription = "Alerta cuando aumentan rebotes o quejas en el configuration set transaccional de SES",
            Metric = sesReputationMetric,
            Threshold = threshold,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });
        sesReputationAlarm.AddAlarmAction(alarmAction);
    }
}
