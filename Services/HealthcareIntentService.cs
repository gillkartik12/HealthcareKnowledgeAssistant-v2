namespace HealthcareKnowledgeAssistant.Services;

public class HealthcareIntentService
{
    public string? DetectDepartment(string question)
    {
        var q = question.ToLowerInvariant();

        if (q.Contains("schedule") ||
            q.Contains("scheduling") ||
            q.Contains("appointment") ||
            q.Contains("booking") ||
            q.Contains("cancel") ||
            q.Contains("reschedule"))
        {
            return "Scheduling";
        }

        if (q.Contains("bill") ||
            q.Contains("billing") ||
            q.Contains("invoice") ||
            q.Contains("payment") ||
            q.Contains("claim") ||
            q.Contains("insurance"))
        {
            return "Billing";
        }

        if (q.Contains("radiology") ||
            q.Contains("mri") ||
            q.Contains("ct") ||
            q.Contains("xray") ||
            q.Contains("scan"))
        {
            return "Radiology";
        }

        if (q.Contains("compliance") ||
            q.Contains("hipaa") ||
            q.Contains("privacy") ||
            q.Contains("security"))
        {
            return "Compliance";
        }

        if (q.Contains("clinical") ||
            q.Contains("patient care") ||
            q.Contains("diagnosis") ||
            q.Contains("treatment"))
        {
            return "Clinical";
        }

        if (q.Contains("operation") ||
            q.Contains("workflow") ||
            q.Contains("process") ||
            q.Contains("procedure"))
        {
            return "Operations";
        }

        return null;
    }
}