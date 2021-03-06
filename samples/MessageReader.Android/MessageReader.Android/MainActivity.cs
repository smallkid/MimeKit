﻿//
// MainActivity.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;

using Android.App;
using Android.Webkit;
using Android.OS;

using MimeKit;
using MimeKit.Text;

namespace MessageReader.Android {
	[Activity (Label = "MessageReader.Android", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		MimeMessage message;
		WebView webView;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our webView from the layout resource
			webView = FindViewById<WebView> (Resource.Id.webView);
			webView.SetInitialScale (50);

			// Load the message
			Message = MimeMessage.Load (GetType ().Assembly.GetManifestResourceStream ("xamarin3.msg"));
		}

		public MimeMessage Message {
			get { return message; }
			set {
				if (value == null)
					throw new ArgumentNullException ("value");

				message = value;

				// render the message body
				Render (message.Body);
			}
		}

		void RenderMultipartRelated (MultipartRelated related)
		{
			var root = related.Root;

			if (root == null)
				return;

			var client = new MultipartRelatedWebViewClient (related);

			webView.SetWebViewClient (client);

			Render (root);
		}

		void RenderText (TextPart text)
		{
			string html;

			if (text.IsHtml) {
				// the text content is already in HTML format
				html = text.Text;
			} else if (text.IsFlowed) {
				var converter = new FlowedToHtml ();
				string delsp;

				// the delsp parameter specifies whether or not to delete spaces at the end of flowed lines
				if (!text.ContentType.Parameters.TryGetValue ("delsp", out delsp))
					delsp = "no";

				if (string.Compare (delsp, "yes", StringComparison.OrdinalIgnoreCase) == 0)
					converter.DeleteSpace = true;

				html = converter.Convert (text.Text);
			} else {
				html = new TextToHtml ().Convert (text.Text);
			}

			webView.LoadData (html, "text/html", "utf-8");
		}

		void Render (MimeEntity entity)
		{
			var related = entity as MultipartRelated;

			if (related != null) {
				RenderMultipartRelated (related);
				return;
			}

			var multipart = entity as Multipart;
			var text = entity as TextPart;

			// check if the entity is a multipart
			if (multipart != null) {
				if (multipart.ContentType.IsMimeType ("multipart", "alternative")) {
					// A multipart/alternative is just a collection of alternate views.
					// The last part is the format that most closely matches what the
					// user saw in his or her email client's WYSIWYG editor.
					TextPart preferred = null;

					for (int i = multipart.Count; i > 0; i--) {
						related = multipart[i - 1] as MultipartRelated;

						if (related != null) {
							var root = related.Root;

							if (root != null && root.ContentType.IsMimeType ("text", "html")) {
								RenderMultipartRelated (related);
								return;
							}

							continue;
						}

						text = multipart[i - 1] as TextPart;

						if (text == null)
							continue;

						if (text.IsHtml) {
							// we prefer html over plain text
							preferred = text;
							break;
						}

						if (preferred == null) {
							// we'll take what we can get
							preferred = text;
						}
					}

					if (preferred != null)
						RenderText (preferred);
				} else if (multipart.Count > 0) {
					// At this point we know we're not dealing with a multipart/related or a
					// multipart/alternative, so we can safely treat this as a multipart/mixed
					// even if it's not.

					// The main message body is usually the first part of a multipart/mixed. I
					// suppose that it might be better to render the first text/* part instead
					// (in case it's not the first part), but that's rare and probably also
					// indicates that the text is meant to be displayed between the other parts
					// (probably images or video?) in some sort of pseudo-multimedia "document"
					// layout. Modern clients don't do this, they use HTML or RTF instead.
					Render (multipart[0]);
				}
			} else if (text != null) {
				// render the text part
				RenderText (text);
			} else {
				// message/rfc822 part
			}
		}
	}
}
