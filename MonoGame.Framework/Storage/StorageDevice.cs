// MIT License - Copyright (C) The Mono.Xna Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

//using System;
//
//namespace Microsoft.Xna.Framework.Storage
//{
//    public class StorageDevice
//    {
//        public bool IsConnected
//        {
//            get
//            {
//                return true;
//            }
//        }
//
//        public StorageContainer OpenContainer(string containerName)
//        {
//            return new StorageContainer(this,containerName);
//        }
//		
//		public static StorageDevice ShowStorageDeviceGuide()
//		{
//			return new StorageDevice();
//		}
//    }
//}

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using MonoGame.Framework.Utilities;

#if WINDOWS_UAP || WINUI
using Windows.Storage;
#else
using System.Runtime.Remoting.Messaging;
#endif

namespace Microsoft.Xna.Framework.Storage
{
	
	// The delegate must have the same signature as the method
	// it will call asynchronously.
	public delegate StorageDevice ShowSelectorAsynchronousShow(PlayerIndex player, int sizeInBytes, int directoryCount);
	// The MonoTouch AOT cannot deal with nullable types in a delegate (or
	// at least not the straightforward implementation), so we define two
	// delegate types.
	public delegate StorageDevice ShowSelectorAsynchronousShowNoPlayer(int sizeInBytes, int directoryCount);

	// The delegate must have the same signature as the method
	// it will call asynchronously.
	public delegate StorageContainer OpenContainerAsynchronous(string displayName);
	
    /// <summary>
    /// Exposes a storage device for storing user data.
    /// </summary>
    /// <remarks>MSDN documentation contains related conceptual article: http://msdn.microsoft.com/en-us/library/bb200105.aspx</remarks>
	public sealed class StorageDevice
	{
		
		PlayerIndex? player;

		int directoryCount;
        private int DirectoryCount { get { return this.directoryCount; } }

		StorageContainer storageContainer;
		
        /// <summary>
        /// Creates a new <see cref="StorageDevice"/> instance.
        /// </summary>
        /// <param name="player">The playerIndex of the player.</param>
        /// <param name="sizeInBytes">Size of the storage device.</param>
        /// <param name="directoryCount"></param>
		internal StorageDevice(PlayerIndex? player, int sizeInBytes, int directoryCount) 
		{
			this.player = player;
			this.directoryCount = directoryCount;
		}
		
        /// <summary>
        /// Returns the amount of free space.
        /// </summary>
		public long FreeSpace
        {
			get
            { 
				// I do not know if the DriveInfo is is implemented on Mac or not
				// thus the try catch
				try
                {
#if WINDOWS_UAP || WINUI
					return long.MaxValue;
#else
                    return new DriveInfo(GetDevicePath).AvailableFreeSpace;
#endif
                }
				catch (Exception)
                {
					StorageDeviceHelper.Path = StorageRoot;
					return StorageDeviceHelper.FreeSpace;
				}
			} 
		}

        /// <summary>
        /// Returns true if device is connected, false otherwise.
        /// </summary>
		public bool IsConnected
        {
			get
            {
				// I do not know if the DriveInfo is is implemented on Mac or not
				// thus the try catch
				try
                {
#if WINDOWS_UAP || WINUI
					return true;
#else
					return new DriveInfo(GetDevicePath).IsReady;
#endif
                }
				catch (Exception)
                {
					return true;
				}
			} 
		}

        /// <summary>
        /// Returns the total size of device.
        /// </summary>
		public long TotalSpace
        {
			get
            {
				
				// I do not know if the DriveInfo is is implemented on Mac or not
				// thus the try catch
				try
                {
#if WINDOWS_UAP || WINUI
					return long.MaxValue;
#else

					// Not sure if this should be TotalSize or TotalFreeSize
					return new DriveInfo(GetDevicePath).TotalSize;
#endif
                }
				catch (Exception)
                {
					StorageDeviceHelper.Path = StorageRoot;
					return StorageDeviceHelper.TotalSpace;
				}
					
			} 
		}
		
		string GetDevicePath
		{
			get
            {
				// We may not need to store the StorageContainer in the future
				// when we get DeviceChanged events working.
				if (storageContainer == null)
                {
					return StorageRoot;
				}
				else
                {
					return storageContainer._storagePath;
				}				
			}
		}

		// TODO: Implement DeviceChanged when we having the graphical implementation

        /// <summary>
        /// Fired when a device is removed or inserted.
        /// </summary>
		public static event EventHandler<EventArgs> DeviceChanged;

        private bool SuppressEventHandlerWarningsUntilEventsAreProperlyImplemented()
        {
            return DeviceChanged != null;
        }

#if WINDOWS_UAP || WINUI
		// Dirty trick to avoid the need to get the delegate from the IAsyncResult (can't be done in WinRT)
		static Delegate showDelegate;
        static Delegate containerDelegate;
#endif

		// Summary:
		//     Begins the process for opening a StorageContainer containing any files for
		//     the specified title.
		//
		// Parameters:
		//   displayName:
		//     A constant human-readable string that names the file.
		//
		//   callback:
		//     An AsyncCallback that represents the method called when the operation is
		//     complete.
		//
		//   state:
		//     A user-created object used to uniquely identify the request, or null.
		public IAsyncResult BeginOpenContainer(string displayName, AsyncCallback callback, object state)
		{
			return OpenContainer(displayName, callback, state);

		}
		
		private IAsyncResult OpenContainer(string displayName, AsyncCallback callback, object state)
		{

#if !ANDROID && !IOS && !TVOS && !NETFX_CORE
            try
            {
                OpenContainerAsynchronous AsynchronousOpen = new OpenContainerAsynchronous(Open);
#if WINDOWS_UAP || WINUI
                containerDelegate = AsynchronousOpen;
#endif
                return AsynchronousOpen.BeginInvoke(displayName, callback, state);
            }
            finally
            {
			}
#else
			var tcs = new TaskCompletionSource<StorageContainer>(state);
            var task = Task.Run<StorageContainer>(() => Open(displayName));
            task.ContinueWith(t =>
            {
                // Copy the task result into the returned task.
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                // Invoke the user callback if necessary.
                if (callback != null)
                    callback(tcs.Task);
            });
            return tcs.Task;
#endif
		}
	
		// Private method to handle the creation of the StorageDevice
		private StorageContainer Open(string displayName) 
		{
			storageContainer = new StorageContainer(this, displayName, this.player);
			return storageContainer;
		}
		
		//
		// Summary:
		//     Begins the process for displaying the storage device selector user interface,
		//     and for specifying a callback implemented when the player chooses a device.
		//     Reference page contains links to related code samples.
		//
		// Parameters:
		//   callback:
		//     An AsyncCallback that represents the method called when the player chooses
		//     a device.
		//
		//   state:
		//     A user-created object used to uniquely identify the request, or null.
		public static IAsyncResult BeginShowSelector(AsyncCallback callback, object state)
		{
			return BeginShowSelector(0, 0, callback, state);
		}

		//
		// Summary:
		//     Begins the process for displaying the storage device selector user interface;
		//     specifies the callback implemented when the player chooses a device. Reference
		//     page contains links to related code samples.
		//
		// Parameters:
		//   player:
		//     The PlayerIndex that represents the player who requested the save operation.
		//     On Windows, the only valid option is PlayerIndex.One.
		//
		//   callback:
		//     An AsyncCallback that represents the method called when the player chooses
		//     a device.
		//
		//   state:
		//     A user-created object used to uniquely identify the request, or null.
		public static IAsyncResult BeginShowSelector(PlayerIndex player, AsyncCallback callback, object state)
		{
			return BeginShowSelector(player, 0, 0, callback, state);
		}
		//
		// Summary:
		//     Begins the process for displaying the storage device selector user interface,
		//     and for specifying the size of the data to be written to the storage device
		//     and the callback implemented when the player chooses a device. Reference
		//     page contains links to related code samples.
		//
		// Parameters:
		//   sizeInBytes:
		//     The size, in bytes, of data to write to the storage device.
		//
		//   directoryCount:
		//     The number of directories to write to the storage device.
		//
		//   callback:
		//     An AsyncCallback that represents the method called when the player chooses
		//     a device.
		//
		//   state:
		//     A user-created object used to uniquely identify the request, or null.
		public static IAsyncResult BeginShowSelector(int sizeInBytes, int directoryCount, AsyncCallback callback, object state)
		{
#if !ANDROID && !IOS && !TVOS && !NETFX_CORE
			var del = new ShowSelectorAsynchronousShowNoPlayer(Show);

#if WINDOWS_UAP || WINUI
            showDelegate = del;
#endif
			return del.BeginInvoke(sizeInBytes, directoryCount, callback, state);
#else
			var tcs = new TaskCompletionSource<StorageDevice>(state);
            var task = Task.Run<StorageDevice>(() => Show(sizeInBytes, directoryCount));
            task.ContinueWith(t =>
            {
                // Copy the task result into the returned task.
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                // Invoke the user callback if necessary.
                if (callback != null)
                    callback(tcs.Task);
            });
            return tcs.Task;
#endif
		}

		
		//
		// Summary:
		//     Begins the process for displaying the storage device selector user interface,
		//     for specifying the player who requested the save operation, for setting the
		//     size of data to be written to the storage device, and for naming the callback
		//     implemented when the player chooses a device. Reference page contains links
		//     to related code samples.
		//
		// Parameters:
		//   player:
		//     The PlayerIndex that represents the player who requested the save operation.
		//     On Windows, the only valid option is PlayerIndex.One.
		//
		//   sizeInBytes:
		//     The size, in bytes, of the data to write to the storage device.
		//
		//   directoryCount:
		//     The number of directories to write to the storage device.
		//
		//   callback:
		//     An AsyncCallback that represents the method called when the player chooses
		//     a device.
		//
		//   state:
		//     A user-created object used to uniquely identify the request, or null.
		public static IAsyncResult BeginShowSelector(PlayerIndex player, int sizeInBytes, int directoryCount, AsyncCallback callback, object state)
		{
#if !ANDROID && !IOS && !TVOS && !NETFX_CORE
			var del = new ShowSelectorAsynchronousShow(Show);
#if WINDOWS_UA
            showDelegate = del;
#endif
            return del.BeginInvoke(player, sizeInBytes, directoryCount, callback, state);
#else
			var tcs = new TaskCompletionSource<StorageDevice>(state);
            var task = Task.Run<StorageDevice>(() => Show(player, sizeInBytes, directoryCount));
            task.ContinueWith(t =>
            {
                // Copy the task result into the returned task.
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                // Invoke the user callback if necessary.
                if (callback != null)
                    callback(tcs.Task);
            });
            return tcs.Task;
#endif
        }
	
		// Private method to handle the creation of the StorageDevice
		private static StorageDevice Show(PlayerIndex player, int sizeInBytes, int directoryCount)
		{
			return new StorageDevice(player, sizeInBytes, directoryCount);
		}

		private static StorageDevice Show(int sizeInBytes, int directoryCount)
		{
			return new StorageDevice(null, sizeInBytes, directoryCount);
		}
		
		//
		//
		// Parameters:
		//   titleName:
		//     The name of the storage container to delete.
		public void DeleteContainer(string titleName)
		{
			throw new NotImplementedException();
		}			

		//
		// Summary:
		//     Ends the process for opening a StorageContainer.
		//
		// Parameters:
		//   result:
		//     The IAsyncResult returned from BeginOpenContainer.
		public StorageContainer EndOpenContainer(IAsyncResult result)
		{

#if !ANDROID && !IOS && !TVOS && !NETFX_CORE
			StorageContainer returnValue = null;
            try
            {
#if WINDOWS_UAP || WINUI
                // AsyncResult does not exist in WinRT
                var asyncResult = containerDelegate as OpenContainerAsynchronous;
				if (asyncResult != null)
				{
					// Wait for the WaitHandle to become signaled.
					result.AsyncWaitHandle.WaitOne();

					// Call EndInvoke to retrieve the results.
    				returnValue = asyncResult.EndInvoke(result);
				}
                containerDelegate = null;
#else
				// Retrieve the delegate.
				AsyncResult asyncResult = result as AsyncResult;
				if (asyncResult != null)
				{
					var asyncDelegate = asyncResult.AsyncDelegate as OpenContainerAsynchronous;

					// Wait for the WaitHandle to become signaled.
					result.AsyncWaitHandle.WaitOne();

					// Call EndInvoke to retrieve the results.
					if (asyncDelegate != null)
						returnValue = asyncDelegate.EndInvoke(result);
				}
#endif
            }
            finally
            {
				// Close the wait handle.
				result.AsyncWaitHandle.Dispose();
			}
			
			return returnValue;
#else
			try
			{
                return ((Task<StorageContainer>)result).Result;
            }
            catch (AggregateException ex)
            {
                throw;
            }
#endif

		}			

		//
		// Summary:
		//     Ends the display of the storage selector user interface. Reference page contains
		//     links to related code samples.
		//
		// Parameters:
		//   result:
		//     The IAsyncResult returned from BeginShowSelector.
		public static StorageDevice EndShowSelector(IAsyncResult result) 
		{

#if !ANDROID && !IOS && !TVOS && !NETFX_CORE
            if (!result.IsCompleted)
            {
				// Wait for the WaitHandle to become signaled.
                try
                {
                    result.AsyncWaitHandle.WaitOne();
                }
                finally
                {
  #if !(WINDOWS_UAP || WINUI)
                    result.AsyncWaitHandle.Close();
  #endif
				}
			}
  #if WINDOWS_UAP || WINUI
            var del = showDelegate;
            showDelegate = null;
  #else
			// Retrieve the delegate.
			AsyncResult asyncResult = (AsyncResult)result;

            var del = asyncResult.AsyncDelegate;
  #endif

			if (del is ShowSelectorAsynchronousShow)
                return (del as ShowSelectorAsynchronousShow).EndInvoke(result);
			else if (del is ShowSelectorAsynchronousShowNoPlayer)
                return (del as ShowSelectorAsynchronousShowNoPlayer).EndInvoke(result);
			else
                throw new ArgumentException("result");
#else
			try
			{
                return ((Task<StorageDevice>)result).Result;
            }
            catch (AggregateException ex)
            {
                throw;
            }
#endif

		}
		
		internal static string StorageRoot
		{
			get
            {
#if WINDOWS_UAP || WINUI
				return ApplicationData.Current.LocalFolder.Path; 
#elif DESKTOPGL
                if(CurrentPlatform.OS == OS.Linux)
                {
                string osConfigDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (String.IsNullOrEmpty(osConfigDir))
                {
                    osConfigDir = Environment.GetEnvironmentVariable("HOME");
                    if (String.IsNullOrEmpty(osConfigDir))
                    {
                        return "."; // Oh well.
                    }
                    osConfigDir += "/.local/share";
                }
                return osConfigDir;
                }
                else if (CurrentPlatform.OS == OS.MacOSX)
                {
                    string osConfigDir = Environment.GetEnvironmentVariable("HOME");
                    if (String.IsNullOrEmpty(osConfigDir))
                    {
                        return "."; // Oh well.
                    }
                    osConfigDir += "/Library/Application Support";
                    return osConfigDir;
                }
                else if(CurrentPlatform.OS == OS.Windows)
                    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                else
                    throw new Exception("Unexpected platform!");
#else
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
            }
		}
	}
}
