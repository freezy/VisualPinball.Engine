namespace VisualPinball.Unity
{
	internal interface IApiInitializable
	{
		void OnInit();
	}

	internal interface IApiHittable
	{
		void OnHit(bool isUnHit = false);
	}

	internal interface IApiRotatable
	{
		void OnRotate(float speed, bool direction);
	}

	internal interface IApiCollidable
	{
		void OnCollide(float hit);
	}

	internal interface IApiSpinnable
	{
		void OnSpin();
	}

	internal interface IApiSlingshot
	{
		void OnSlingshot();
	}
}
