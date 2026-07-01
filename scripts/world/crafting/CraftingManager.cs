using System.Collections.Generic;
using Godot;

public partial class CraftingManager : Node
{
    private readonly List<CraftJob> _jobs = new();
    private int _nextJobId = 1;

    public IReadOnlyList<CraftJob> GetAllJobs()
    {
        return _jobs;
    }

    public bool TryCreateJob(string recipeId, Vector2I facilityCell, out CraftJob? job)
    {
        job = null;

        if (!CraftRecipeDatabase.TryGet(recipeId, out CraftRecipeEntry recipe) || !recipe.IsEnabled)
        {
            return false;
        }

        if (TryGetJobAtFacility(facilityCell, out _))
        {
            return false;
        }

        CraftJob createdJob = new()
        {
            JobId = _nextJobId++,
            RecipeId = recipe.RecipeId,
            FacilityCell = facilityCell,
            RequiredWork = recipe.RequiredWork
        };
        createdJob.SetRequirements(recipe);

        _jobs.Add(createdJob);
        job = createdJob;
        return true;
    }

    public bool CancelJob(CraftJob job)
    {
        if (job == null || job.IsCompleted || job.IsCancelled)
        {
            return false;
        }

        job.Cancel();
        return true;
    }

    public bool TryGetJobAtFacility(Vector2I facilityCell, out CraftJob? job)
    {
        foreach (CraftJob candidate in _jobs)
        {
            if (candidate.FacilityCell == facilityCell && !candidate.IsCompleted && !candidate.IsCancelled)
            {
                job = candidate;
                return true;
            }
        }

        job = null;
        return false;
    }

    public bool TryGetJobById(int jobId, out CraftJob? job)
    {
        foreach (CraftJob candidate in _jobs)
        {
            if (candidate.JobId == jobId)
            {
                job = candidate;
                return true;
            }
        }

        job = null;
        return false;
    }

    public IReadOnlyList<CraftJob> GetJobsForRecipe(string recipeId)
    {
        List<CraftJob> jobs = new();

        foreach (CraftJob job in _jobs)
        {
            if (job.RecipeId == recipeId)
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    public void ValidateJobs()
    {
        foreach (CraftJob job in _jobs)
        {
            job.PruneReservations();
        }
    }
}
