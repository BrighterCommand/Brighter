/// >> amplify.request

var amplify = {

	request: function( resourceId ) {
		/// <field name="decoders" type="Object">Decoders allow you to parse an ajax response before calling the success or error callback. This allows you to return data marked with a status and react accordingly. This also allows you to manipulate the data any way you want before passing the data along to the callback.</field>
		/// <summary>
		/// 	Request a resource.
		/// 	&#10;Additional Signatures:
		/// 	&#10;&#09;1. amplify.request( resourceId [, data [, callback ]] )
		/// 	&#10;&#09;2. amplify.request( settings* )
		/// 	&#10;&#10;API Reference: http://amplifyjs.com/api/request
		/// </summary>
		///	<param name="resourceId" type="String">Identifier string for the resource.</param>
		///	<param name="data" type="Object" optional="true">An object literal of data to be sent to the resource.</param>
		///	<param name="callback" type="Function" optional="true">A function to call once the resource has been retrieved.</param>
		/// <param name="settings*" type="Hash">
		///	* This parameter is only used with the #2 signature. A set of key/value pairs of settings for the request.
		/// &#10;&#09;{
		/// &#10;&#09;resourceId: Identifier string for the resource.
		/// &#10;&#09;&#09;data (optional): Data associated with the request.
		/// &#10;&#09;&#09;success (optional): Function to invoke on success.
		/// &#10;&#09;&#09;error (optional): Function to invoke on error.		
		/// &#10;&#09;}
		///</param>
		/// <returns type="Object">An object containing an 'abort' method</returns>
	}

};

// stub for vsdoc
amplify.request.decoders = { };

amplify.request.define = function( resourceId, requestType, settings ) {
	/// <summary>
	/// 	Define a resource.
	/// 	&#10;Additional Signatures:
	/// 	&#10;&#09;1. amplify.request.define( resourceId* , resource* )
	/// 	&#10;&#10;API Reference: http://amplifyjs.com/api/request/#usage
	/// </summary>
	///	<param name="resourceId" type="String">Identifier string for the resource.</param>
	///	<param name="requestType" type="String">The type of data retrieval method from the server. See http://amplifyjs.com/api/request/#request_types for more information.</param>
	///	<param name="settings" type="Hash">
	///	A set of key/value pairs that relate to the server communication technology. Any settings found in jQuery.ajax() may be applied to this object.
	/// &#10;&#09;{
	/// &#10;&#09;&#09;cache: see the http://amplifyjs.com/api/request/#cache for more details.
	/// &#10;&#09;&#09;decoder: see amplify.request.decoders for more details.
	/// &#10;&#09;}
	/// </param>
	///	<param name="resourceId*" type="String">* This parameter is only used with the #1 additional signature. Identifier string for the resource.</param>
	///	<param name="resource*" type="Function"> 
	/// * This parameter is only used with the #1 additional signature. Function to handle requests. Receives a hash with the following properties:
	/// &#10;&#09;{
	/// &#10;&#09;&#09;resourceId: Identifier string for the resource.
	/// &#10;&#09;&#09;data: Data provided by the user.
	/// &#10;&#09;&#09;success: Callback to invoke on success.
	/// &#10;&#09;&#09;error: Callback to invoke on error.
	/// &#10;&#09;}
	/// </param>
};

/// >> amplify.store

amplify.store = function( key, value, options ) {

	/// <summary>
	/// 	Stores a value for a given key using the default storage type.
	/// 	&#10;Additional Signatures:
	/// 	&#10;&#09;1. amplify.store() - Gets a hash of all stored values.
	/// 	&#10;&#09;2.	amplify.store( string key ) - Gets a stored value based on the key.
	/// 	&#10;&#09;3. amplify.store( string key, null ) - Clears key/value pair from the store.
	/// 	&#10;&#10;API Reference: http://amplifyjs.com/api/store
	/// </summary>
	/// <param name="key" type="String">Identifier for the value being stored.</param>
	/// <param name="value" type="Mixed">The value to store. The value can be anything that can be serialized as JSON.</param>
	/// <param name="options" type="Hash" optional="true">A set of key/value pairs that relate to settings for storing the value.</param>
	/// <returns type="Object">Depending on the signature used, the method will return the original object/data stored, or undefined.</returns>
};

/// >> amplify.pub/sub

amplify.subscribe = function( topic, callback ) {
	/// <summary>
	/// 	Subscribe to a message
	/// 	&#10;Additional Signatures:
	/// 	&#10;&#09;1. amplify.subscribe( topic, context, callback )
	/// 	&#10;&#09;2. amplify.subscribe( topic, callback, priority )
	/// 	&#10;&#09;3. amplify.subscribe( topic, context, callback, priority )
	/// 	&#10;&#10;API Reference: http://amplifyjs.com/api/pubsub
	/// </summary>
	/// <param name="topic" type="String">Name of the message to subscribe to.</param>
	/// <param name="context" type="Object" optional="true">What this will be when the callback is invoked.</param>
	/// <param name="callback" type="Function">Function to invoke when the message is published.</param>
	/// <param name="priority" type="Number" optional="true">Priority relative to other subscriptions for the same message. Lower values have higher priority. Default is 10.</param>
};
 
amplify.unsubscribe = function( topic, callback ) {
	/// <summary>Remove a subscription.</summary>
	/// <param name="topic" type="String">The topic being unsubscribed from.</param>
	/// <param name="callback" type="Function">The callback that was originally subscribed.</param>
};

amplify.publish = function( topic ) {
	/// <summary>
	/// Publish a message.
	/// &#10;Any number of additional parameters can be passed to the subscriptions
	/// &#10;&#10;API Reference: http://amplifyjs.com/api/pubsub
	/// </summary>
	/// <param name="topic" type="String">The name of the message to publish.</param>
	/// <returns type="Boolean">amplify.publish returns a boolean indicating whether any subscriptions returned false. The return value is true if none of the subscriptions returned false, and false otherwise. Note that only one subscription can return false because doing so will prevent additional subscriptions from being invoked.</returns>
};