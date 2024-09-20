using AttendanceAPIV2.Interfces;
using OpenCvSharp;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AttendanceAPIV2.Services
{
    public class FaceRecognitionService : IFaceRecognitionService
    {
        //	//public async Task<bool> CompareFacesAsync(string imagePath1, string imagePath2)
        //	//{
        //	//	// Load both images
        //	//	using var image1 = new Mat(imagePath1, ImreadModes.Color);
        //	//	using var image2 = new Mat(imagePath2, ImreadModes.Color);

        //	//	// Convert to grayscale for easier comparison
        //	//	var gray1 = new Mat();
        //	//	var gray2 = new Mat();
        //	//	Cv2.CvtColor(image1, gray1, ColorConversionCodes.BGR2GRAY);
        //	//	Cv2.CvtColor(image2, gray2, ColorConversionCodes.BGR2GRAY);

        //	//	// Face detection using Haar Cascade
        //	//	var faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");

        //	//	var faces1 = faceCascade.DetectMultiScale(gray1);
        //	//	var faces2 = faceCascade.DetectMultiScale(gray2);

        //	//	// Compare the first detected face in each image
        //	//	if (faces1.Length > 0 && faces2.Length > 0)
        //	//	{
        //	//		var face1 = new Mat(gray1, faces1[0]);
        //	//		var face2 = new Mat(gray2, faces2[0]);

        //	//		// Compute absolute difference between the two face images
        //	//		var diff = new Mat();
        //	//		Cv2.Absdiff(face1, face2, diff);

        //	//		// Compute the sum of differences in pixel values (for grayscale, [0] should be sufficient)
        //	//		Scalar sumOfDifferences = Cv2.Sum(diff);

        //	//		// The first channel represents intensity differences in grayscale
        //	//		double result = sumOfDifferences.Val0;

        //	//		// Set a reasonable threshold for matching (you may need to fine-tune this value)
        //	//		return result < 1000;  // Adjust threshold as necessary
        //	//	}
        //	//	return false;  // No faces detected or not matching
        //	//}
    }


}

