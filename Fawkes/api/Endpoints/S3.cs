using Amazon.S3;
using Amazon.S3.Model;
using Fawkes.Api.Settings;
using Microsoft.AspNetCore.Mvc;
using Voyager;

namespace Fawkes.Api.Endpoints;

[VoyagerEndpoint("/api/s3/{id}")]
public class S3
{
	public record Request([FromRoute] string id);

	public async Task<IResult> Delete(Request request, AppSettings appSettings)
	{
		if (appSettings.S3Locations.ContainsKey(request.id))
		{
			appSettings.S3Locations.Remove(request.id);
			await appSettings.Save();
		}
		return TypedResults.Ok();
	}

	public IResult Get(Request request, AppSettings appSettings)
	{
		if (appSettings.S3Locations.TryGetValue(request.id, out var location))
		{
			return TypedResults.Ok(location);
		}
		return TypedResults.NotFound();
	}

	public record AddS3Request([FromRoute] string id, [FromBody] S3BackupLocation config);

	public async Task<IResult> Post(AddS3Request request, AppSettings appSettings)
	{
		using var client = new AmazonS3Client(
			new Amazon.Runtime.BasicAWSCredentials(request.config.AccessKey, request.config.SecretKey),
			new AmazonS3Config
			{
				ServiceURL = request.config.Endpoint,
				ForcePathStyle = request.config.ForcePathStyle ?? false
			}
		);
		try
		{
			var response = await client.GetBucketLocationAsync(new GetBucketLocationRequest
			{
				BucketName = request.config.Bucket
			});
			request.config.Id = request.id;
			appSettings.S3Locations.Add(request.id, request.config);
			await appSettings.Save();
		}
		catch (AmazonS3Exception exception)
		{
			return TypedResults.Problem(title: exception.Message);
		}
		catch
		{
			throw;
		}
		return TypedResults.Ok();
	}
}

[VoyagerEndpoint("/api/s3")]
public class GetAllS3()
{
	public IResult Get(AppSettings appSettings)
	{
		return TypedResults.Ok(appSettings.S3Locations);
	}
}