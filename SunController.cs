using System;
using System.Collections;
using UnityEngine;


namespace SunController
{
    /// <summary>
    /// Controls the motion, color, and light intensity of an object like a sun, or moon
    /// 'Sun' is used to describe this objects in comments for readability)
    /// </summary>
    [RequireComponent(typeof(Light))]
    [Serializable]
    public class SunController : MonoBehaviour
    {
        /// <summary>
        /// Sun's current speed
        /// </summary>
        public float RotationStep;

        /// <summary>
        /// Sun's desired angle (0-179)
        /// Can be an approximation of the sun's actual rotation
        /// </summary>
        public float Rotated;

        /// <summary>
        /// Target color for Lerping.
        /// Stored publicly for serialization
        /// </summary>
        public Color DesiredColor;

        /// <summary>
        /// Target intensity for Lerping.
        /// Stored publicly for serialization
        /// </summary>
        public float DesiredIntensity;

        /// <summary>
        /// Current color of the Sun
        /// Stored publicly for serialization
        /// </summary>
        public Color CurrentColor;

        /// <summary>
        /// Current Intensity of the Sun
        /// Stored publicly for serialization
        /// </summary>
        public float CurrentIntensity;

        /// <summary>
        /// Sun's color at the start of Dawn
        /// </summary>
        public Color DawnColor = new Color(255 / 255f, 217 / 255f, 208 / 255f);

        /// <summary>
        /// Sun's color during regular daylight hours
        /// </summary>
        public Color DayColor = new Color(255 / 255f, 217 / 255f, 208 / 255f);

        /// <summary>
        /// Sun's color at the end of Dusk
        /// </summary>
        public Color DuskColor = new Color(255 / 255f, 217 / 255f, 208 / 255f);

        /// <summary>
        /// The intensity of 12:00 PM daylight
        /// </summary>
        public float DayIntensity = 0.5f;

        /// <summary>
        /// Hour of day that Dawn should occur (0-23)
        /// Dawn will not occur before this hour, even if the "day" has finished
        /// This setting, along with DayLength, can be used to adjust the amount of daylight hours in a 24-hr period
        /// </summary>
        public int DawnHour = 6;

        /// <summary>
        /// How long should the transitions be between colors
        /// Scaled based on game speed, and day length;
        /// </summary>
        public float ColorTransitionTime = 15f;
        /// <summary>
        /// How long should the transitions be between intensities
        /// Scaled based on game speed, and day length;
        /// </summary>
        public float IntensityTransitionTime = 15f;

        /// <summary>
        /// Modifies the length of the day. Used for increasing or decreasing day/night cycle without affecting game speed
        /// Default is 12 hours of daylight, increase for more sunlight, or decrease for longer nights
        /// This setting, along with DawnHour, can be used to adjust the amount of daylight hours in a 24-hr period
        /// </summary>
        public float DayLength = 1;

        /// <summary>
        /// Has the sun been started up?
        /// </summary>
        public bool Init;

        /// <summary>
        /// State the sun currently is in
        /// </summary>
        public SunStates SunState;

        /// <summary>
        /// Stores rotational amounts until ready to apply them
        /// </summary>
        private float _updateBuffer;

        public float SunUpdateThreshold = 1f;

        /// <summary>
        /// Possible states the sun can be in
        /// </summary>
        public enum SunStates
        {
            Dawn,
            Day,
            Dusk,
            Night
        }

        //The light attached to this object
        private Light _light;

        [UsedImplicitly]
        public void Start()
        {
            _light = transform.GetComponent<Light>();

            //Has game been started before, or is it a new game?
            if (!Init)
            {
                //New Game
                StartCoroutine(TriggerDawn());
                Init = true;
            }
            else
            {
                //Save game
                //Perform any operations to restore from save

                //Reset the sun's rotation to where it was last
                transform.rotation = Quaternion.Euler(Rotated, 0, 0);

                //Check if we need to restart the lerp coroutines 
                if (DesiredColor != CurrentColor)
                {
                    StartCoroutine(LerpColor());
                }
                if (Math.Abs(DesiredIntensity - CurrentIntensity) > 0.001)
                {
                    StartCoroutine(LerpIntensity());
                }

            }
        }

        [UsedImplicitly]
        public void Update()
        {
            //Determine how much we should rotate this frame, and save it to our Rotated tracking var
            RotationStep = ((Time.deltaTime * (Main.TimeState.Speed / 60)) / 4) * DayLength;
            Rotated += RotationStep;

            //We don't want to update the sun's rotation every frame, nor every very small change. 
            //Save updates here, until they reach SunUpdateThreshold (logic below)
            _updateBuffer += RotationStep;

            //Actually Rotate the light object around X axis until it reaches the edge of the Western horizon (sunset, 179 degrees)
            //After that point, it is nighttime so no need to actually rotate the object, we just track it in the Rotated var
            //Only update when enough updates are stored up to make meaningful updates
            if (Rotated < 179 && _updateBuffer >= SunUpdateThreshold)
            {
                transform.rotation = Quaternion.Euler(Rotated, 0, 0);
                _updateBuffer = 0;
            }

            //When the sun reaches the latter part of it's 180 degree rotation, trigger dusk (if it's currently day).
            if (Rotated > 150 && SunState == SunStates.Day)
            {
                StartCoroutine(TriggerDusk());
            }

            //When the sun has made a full rotation around again, trigger dawn (if it's nighttime)
            //Will reset the sun to 0 X rotation (from 179)
            if (Rotated >= 360 && SunState == SunStates.Night)
            {
                StartCoroutine(TriggerDawn());
            }

            //Apply any changes made in the Dawn/Dusk coroutines to the actual light object
            _light.color = CurrentColor;
            _light.intensity = CurrentIntensity;
        }

        /// <summary>
        /// Resets the sun, and triggers the Dawn->Daytime transition.
        /// </summary>
        /// <returns>SunState will be Day once completed</returns>
        public IEnumerator TriggerDawn()
        {
            //Zero out to reset the sun, and set to dawn color
            Rotated = 0;
            transform.rotation = Quaternion.Euler(0, 0, 0);

            //Set values to Dawn colors, transitioning to Daytime
            CurrentColor = DawnColor;
            CurrentIntensity = 0f;
            DesiredColor = DayColor;
            DesiredIntensity = DayIntensity;
            SunState = SunStates.Dawn;

            //Check if we need to wait for it to be DawnHour (correct time for sun to rise)
            while (Main.TimeState.CurrentTime.Hour < DawnHour) yield return null;

            //Initiate Dawn
            StartCoroutine(LerpIntensity());
            yield return StartCoroutine(LerpColor());
        }

        /// <summary>
        /// Starts the transition to dusk and nighttime
        /// </summary>
        /// <returns>SunState will be Night when completed</returns>
        public IEnumerator TriggerDusk()
        {
            //Start the transition to Dusk/Night
            DesiredColor = DuskColor;
            DesiredIntensity = 0f;
            SunState = SunStates.Dusk;
            StartCoroutine(LerpColor());
            yield return StartCoroutine(LerpIntensity());
        }

        /// <summary>
        /// Lerps CurrentColor from CurrentColor to DesiredColor over ColorTransitionTime
        /// </summary>
        /// <returns>CurrentColor=DesiredColor upon completion</returns>
        private IEnumerator LerpColor()
        {
            float lerpTime = 0;
            Color startingColor = CurrentColor;

            while (lerpTime < ColorTransitionTime)
            {
                CurrentColor = Color.Lerp(startingColor, DesiredColor, lerpTime / ColorTransitionTime);
                lerpTime += RotationStep;
                yield return true;
            }

            //Done lerping, set final value
            CurrentColor = DesiredColor;

            //Color changed, advance to next sun state
            SunState++;
        }

        /// <summary>
        /// Lerps CurrentIntensity from CurrentIntensity to DesiredIntensity over IntensityTransitionTime
        /// </summary>
        /// <returns>CurrentIntensity=DesiredIntensity upon completion</returns>
        private IEnumerator LerpIntensity()
        {
            float lerpTime = 0;
            float startingIntensity = CurrentIntensity;

            while (lerpTime < IntensityTransitionTime)
            {

                CurrentIntensity = Mathf.Lerp(startingIntensity, DesiredIntensity, lerpTime / IntensityTransitionTime);
                lerpTime += RotationStep;
                yield return true;
            }

            //Done lerping, set final value
            CurrentIntensity = DesiredIntensity;
        }

    }
}